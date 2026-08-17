// Build-time Mermaid diagrams keep their intrinsic SVG size, which gets small
// for wide graphs: clicking one opens it scaled to the viewport instead.
function close() {
  document.querySelector('.sc-lightbox')?.remove();
  document.documentElement.classList.remove('sc-lightbox-open');
}

document.addEventListener('click', (event) => {
  if (!(event.target instanceof Element)) return;
  const img = event.target.closest('.mermaid-diagram img');
  if (!img) return;

  // The clicked img is whichever theme variant is visible; the clone drops the
  // variant class and id so the theme-switching CSS cannot hide it again.
  const zoomed = img.cloneNode();
  zoomed.removeAttribute('class');
  zoomed.removeAttribute('id');

  const overlay = document.createElement('div');
  overlay.className = 'sc-lightbox';
  overlay.append(zoomed);
  overlay.addEventListener('click', close);
  document.body.append(overlay);
  document.documentElement.classList.add('sc-lightbox-open');
});

document.addEventListener('keydown', (event) => {
  if (event.key === 'Escape') close();
});
