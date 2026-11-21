using mjxClothTool.Views;

namespace mjxClothTool.UnitTests.Views;

public class PropConverterSelectionTests
{
    [Fact]
    public void UserTargetSlotChangeUpdatesOnlySelectedFiles()
    {
        var active = new PropConverterWindow.ProjectFile { SlotIndex = 0 };
        var selected = new PropConverterWindow.ProjectFile { SlotIndex = 1 };
        var unselected = new PropConverterWindow.ProjectFile { SlotIndex = 2 };

        PropConverterWindow.ApplyTargetSlotToSelection([active, selected], 4, isProgrammaticLoad: false);

        Assert.Equal(4, active.SlotIndex);
        Assert.Equal(4, selected.SlotIndex);
        Assert.Equal(2, unselected.SlotIndex);
    }

    [Fact]
    public void LoadingPreviewSlotDoesNotUpdateSelectedFiles()
    {
        var first = new PropConverterWindow.ProjectFile { SlotIndex = 0 };
        var second = new PropConverterWindow.ProjectFile { SlotIndex = 2 };

        PropConverterWindow.ApplyTargetSlotToSelection([first, second], 3, isProgrammaticLoad: true);

        Assert.Equal(0, first.SlotIndex);
        Assert.Equal(2, second.SlotIndex);
    }

    [Fact]
    public void EmptySelectionAndInvalidSlotAreIgnored()
    {
        PropConverterWindow.ApplyTargetSlotToSelection([], 3, isProgrammaticLoad: false);
        var file = new PropConverterWindow.ProjectFile { SlotIndex = 1 };

        PropConverterWindow.ApplyTargetSlotToSelection([file], -1, isProgrammaticLoad: false);

        Assert.Equal(1, file.SlotIndex);
    }
}
