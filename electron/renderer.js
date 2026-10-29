function createBrowserModel(snapshot) {
  const model = {
    snapshot,
    addonIndex: 0,
    selectedDrawableId: null,
    get drawables() { return snapshot.addons?.[this.addonIndex]?.drawables ?? []; },
    get selectedDrawable() { return this.drawables.find(item => item.id === this.selectedDrawableId) ?? null; },
    selectAddon(index) { this.addonIndex = index; this.selectedDrawableId = null; },
    selectDrawable(id) { this.selectedDrawableId = id; }
  };
  return model;
}

if (typeof module !== 'undefined') module.exports = { createBrowserModel };

if (typeof document !== 'undefined') {
  let model = null;
  const $ = id => document.getElementById(id);
  const make = (tag, className, text) => {
    const element = document.createElement(tag);
    element.className = className;
    element.textContent = text;
    return element;
  };

  function render() {
    $('projectName').textContent = model?.snapshot.projectName || 'No project open';
    $('addonList').replaceChildren();
    $('drawableList').replaceChildren();
    $('textureList').replaceChildren();
    if (!model) return;

    (model.snapshot.addons || []).forEach((addon, index) => {
      const button = make('button', 'addon-tab' + (index === model.addonIndex ? ' active' : ''), addon.name);
      button.type = 'button';
      button.onclick = () => { model.selectAddon(index); render(); };
      $('addonList').append(button);
    });

    model.drawables.forEach(drawable => {
      const button = make('button', 'drawable-row' + (drawable.id === model.selectedDrawableId ? ' active' : ''), drawable.displayName || drawable.name);
      button.type = 'button';
      button.onclick = () => { model.selectDrawable(drawable.id); render(); };
      $('drawableList').append(button);
    });

    const selected = model.selectedDrawable;
    $('selectedName').textContent = selected?.displayName || selected?.name || 'Select a drawable';
    $('selectedMeta').textContent = selected ? `${selected.isProp ? 'Prop' : 'Component'} ${selected.typeNumeric} · ${selected.sex === 1 ? 'Male' : 'Female'} · ${selected.textures?.length ?? 0} textures` : 'Choose an item from the list';
    (selected?.textures || []).forEach((texture, index) => {
      const row = make('div', 'texture-row', '');
      row.append(make('span', 'texture-index', String.fromCharCode(97 + index)));
      row.append(make('span', 'texture-name', texture.originalFileName || texture.displayName || texture.filePath || 'Unnamed texture'));
      const size = texture.txtDetails ? `${texture.txtDetails.width}×${texture.txtDetails.height}` : 'Unknown size';
      row.append(make('span', 'texture-meta', size));
      $('textureList').append(row);
    });
  }

  document.addEventListener('DOMContentLoaded', () => {
    $('version').textContent = `Electron ${window.mjx.getVersion()} · Read-only preview`;
    $('openProject').addEventListener('click', async () => {
      $('status').textContent = 'Opening project…';
      try {
        const snapshot = await window.mjx.openProject();
        if (snapshot) { model = createBrowserModel(snapshot); render(); $('status').textContent = 'Project opened read-only'; }
        else $('status').textContent = 'Open canceled';
      } catch (error) { $('status').textContent = error.message; }
    });
    render();
  });
}
