using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Vector3 = SharpDX.Vector3;

namespace mjxClothTool.Views;

public partial class PropConverterWindow
{
    private static readonly Brush[] AxisColors = [Brushes.OrangeRed, Brushes.LimeGreen, Brushes.DodgerBlue];
    private static readonly Vector3D[] LocalAxes = [new(1,0,0), new(0,1,0), new(0,0,1)];
    private readonly Vector[] _projected = new Vector[3];
    private Transform3D _placement = Transform3D.Identity;
    private Vector3 _bakedOffset;
    private bool _canMove;
    private record Handle(int A, int B = -1);
    private record Drag(Handle Handle, Point Start, Vector U, Vector V, Vector3 Offset);
    private Drag _drag;

    private static Model3DGroup SolidAccessory(Model3DGroup model)
    {
        var solid = new Model3DGroup { Transform = model.Transform };
        var material = new DiffuseMaterial(Brushes.LightSkyBlue);
        foreach (GeometryModel3D part in model.Children)
            solid.Children.Add(new GeometryModel3D(part.Geometry, material)
                { BackMaterial = material, Transform = part.Transform });
        solid.Freeze();
        return solid;
    }

    private void ClearGizmo()
    {
        _drag = null;
        if (Axes.IsMouseCaptured) Axes.ReleaseMouseCapture();
        _canMove = false;
        Highlight.Content = null;
        Axes.Children.Clear();
    }

    private void Reveal_Click(object sender, RoutedEventArgs e) =>
        RevealView.Visibility = Reveal.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void FitAccessory_Click(object sender, RoutedEventArgs e)
    {
        if (Mesh.Content != null) Fit(Mesh.Transform.TransformBounds(Mesh.Content.Bounds));
    }

    private void AxesSizeChanged(object sender, SizeChangedEventArgs e) => DrawAxes();

    private void DrawAxes()
    {
        if (Axes == null) return;
        Axes.Children.Clear();
        if (!_canMove || Mesh.Content == null || Axes.ActualWidth == 0) return;
        var b = Mesh.Content.Bounds;
        var center = Mesh.Transform.Transform(new Point3D(b.X+b.SizeX/2, b.Y+b.SizeY/2, b.Z+b.SizeZ/2));
        var forward = Camera.LookDirection;
        forward.Normalize();
        var right = Vector3D.CrossProduct(forward, Camera.UpDirection);
        right.Normalize();
        var up = Vector3D.CrossProduct(right, forward);
        double pixels = Axes.ActualWidth / Camera.Width;
        Vector Project(Vector3D d) => pixels * new Vector(Vector3D.DotProduct(d,right), -Vector3D.DotProduct(d,up));
        var origin = new Point(Axes.ActualWidth/2, Axes.ActualHeight/2) + Project(center - _focus);
        var arrows = new Vector[3];
        for (int i=0; i<3; i++)
        {
            _projected[i] = Project(_placement.Transform(LocalAxes[i]));
            arrows[i] = _projected[i] * (67 / pixels);
        }
        for (int i = 0; i < 3; i++)
        {
            int j = (i + 1) % 3;
            var u = arrows[i]; var v = arrows[j];
            if (Math.Abs(Vector.CrossProduct(u, v)) < 650) continue;
            var a = origin + 0.22 * (u + v);
            var plane = new Polygon { Points = [a,a+u*.17,a+u*.17+v*.17,a+v*.17], Fill = AxisColors[3-i-j],
                Opacity = 0.75, Tag = new Handle(i,j), Cursor = Cursors.SizeAll, ToolTip = $"Move {"XYZ"[i]} / {"XYZ"[j]}" };
            Axes.Children.Add(plane);
        }
        for (int i = 0; i < 3; i++)
        {
            var arrow = arrows[i];
            if (arrow.Length < 4) continue; // Viewed end-on; orbit to expose this axis.
            var tip = origin + arrow;
            var unit = arrow / arrow.Length;
            var side = new Vector(-unit.Y, unit.X);
            var handle = new Handle(i);
            var shaft = new Line { X1=origin.X,Y1=origin.Y,X2=tip.X,Y2=tip.Y,Stroke=AxisColors[i],StrokeThickness=3,IsHitTestVisible=false };
            Axes.Children.Add(shaft);
            Axes.Children.Add(new Line { X1=origin.X,Y1=origin.Y,X2=tip.X,Y2=tip.Y,Stroke=Brushes.Transparent,StrokeThickness=12,Tag=handle,Cursor=Cursors.Hand });
            Axes.Children.Add(new Polygon { Points=[tip,tip-11*unit+4.5*side,tip-11*unit-4.5*side],Fill=AxisColors[i],Tag=handle,Cursor=Cursors.Hand });
            var label = new TextBlock { Text="XYZ"[i].ToString(),Foreground=AxisColors[i], FontWeight=FontWeights.Bold, Tag=handle, Cursor=Cursors.Hand };
            Canvas.SetLeft(label,tip.X+unit.X*9-5); Canvas.SetTop(label,tip.Y+unit.Y*9-9);
            Axes.Children.Add(label);
        }
    }

    private void AxisDown(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.Tag is not Handle handle || !_canMove || _drag != null) return;
        if (!Axes.CaptureMouse()) return;
        _drag = new(handle,e.GetPosition(Axes),_projected[handle.A],handle.B < 0 ? default : _projected[handle.B],_bakedOffset);
        _revision++;
        _refresh.Stop();
        _preview = null;
        Export.IsEnabled = false;
        e.Handled = true;
    }

    private void AxisMove(object sender, MouseEventArgs e)
    {
        if (_drag == null) return;
        MoveTo(e.GetPosition(Axes));
        e.Handled = true;
    }

    private void MoveTo(Point position)
    {
        var d = _drag;
        var displacement = position - d.Start;
        var offset = d.Offset;
        if (d.Handle.B < 0)
            offset[d.Handle.A] += (float)(Vector.Multiply(displacement,d.U) / d.U.LengthSquared);
        else
        {
            var det = Vector.CrossProduct(d.U, d.V);
            offset[d.Handle.A] += (float)(Vector.CrossProduct(displacement,d.V) / det);
            offset[d.Handle.B] += (float)(Vector.CrossProduct(d.U,displacement) / det);
        }
        X.Text = offset.X.ToString("G9", CultureInfo.CurrentCulture);
        Y.Text = offset.Y.ToString("G9", CultureInfo.CurrentCulture);
        Z.Text = offset.Z.ToString("G9", CultureInfo.CurrentCulture);
        var delta = offset - _bakedOffset;
        var matrix = Matrix3D.Identity;
        matrix.Translate(new Vector3D(delta.X,delta.Y,delta.Z));
        matrix.Append(_placement.Value);
        Highlight.Transform = Mesh.Transform = new MatrixTransform3D(matrix);
        DrawAxes();
    }

    private void AxisUp(object sender, MouseButtonEventArgs e)
    {
        if (_drag == null) return;
        MoveTo(e.GetPosition(Axes));
        Axes.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void AxisLostCapture(object sender, MouseEventArgs e)
    {
        if (_drag == null) return;
        _drag = null;
        InputChanged(this,null);
    }
}
