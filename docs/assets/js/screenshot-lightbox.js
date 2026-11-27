// Lightbox for documentation screenshots and images. No library.
// Screenshots (_includes/screenshot.html) are links to their full-size image, so without JavaScript the link simply
// opens the file. Other content images (Markdown ![...]) that are not links and are at least 200px wide become
// keyboard-focusable buttons (Enter/Space). Both open a modal <dialog>: Esc, the close button or a click outside
// closes it, focus stays inside it while open (showModal) and returns to the image afterwards.
(function () {
  'use strict';
  var dialog, image, caption, opener;

  function build() {
    dialog = document.createElement('dialog');
    dialog.className = 'screenshot-lightbox';
    dialog.setAttribute('aria-label', 'Screenshot');
    dialog.innerHTML =
      '<button type="button" class="screenshot-lightbox-close" aria-label="Close">×</button>' +
      '<img alt=""><p class="screenshot-lightbox-caption"></p>';
    image = dialog.querySelector('img');
    caption = dialog.querySelector('.screenshot-lightbox-caption');
    dialog.querySelector('.screenshot-lightbox-close').addEventListener('click', function () { dialog.close(); });
    // A click on the backdrop (the dialog element itself, outside the image) closes it.
    dialog.addEventListener('click', function (e) { if (e.target === dialog) { dialog.close(); } });
    dialog.addEventListener('close', function () {
      document.documentElement.classList.remove('screenshot-lightbox-open');
      if (opener) { opener.focus(); }
    });
    document.body.appendChild(dialog);
  }

  function open(trigger, src, img, text) {
    if (!dialog) { build(); }
    opener = trigger;
    image.src = src;
    image.alt = img ? img.alt : '';
    caption.textContent = text || (img ? img.alt : '');
    document.documentElement.classList.add('screenshot-lightbox-open');
    dialog.showModal(); // Esc closes a modal dialog natively
  }

  document.addEventListener('click', function (e) {
    if (e.ctrlKey || e.metaKey || e.shiftKey || e.button !== 0) { return; }
    if (typeof HTMLDialogElement !== 'function') { return; } // old browser: follow the link
    var link = e.target.closest && e.target.closest('a.screenshot-link');
    if (link) {
      e.preventDefault();
      var img = link.querySelector('img');
      var figcaption = link.parentNode.querySelector('figcaption');
      open(link, link.href, img, figcaption ? figcaption.textContent : '');
      return;
    }
    var zoom = e.target.closest && e.target.closest('img.content-zoom');
    if (zoom) { e.preventDefault(); open(zoom, zoom.currentSrc || zoom.src, zoom, ''); }
  });

  // plain content images: focusable, open with Enter/Space
  function enableImages() {
    if (typeof HTMLDialogElement !== 'function') { return; }
    document.querySelectorAll('#main-content main img').forEach(function (img) {
      if (img.closest('a, .screenshot, .spectra-visual, .spectra-card, .spectra-terminal')) { return; }
      var mark = function () {
        if ((img.naturalWidth || img.width) < 200) { return; }
        img.classList.add('content-zoom');
        img.setAttribute('tabindex', '0');
        img.setAttribute('role', 'button');
        img.setAttribute('aria-label', 'Open full size: ' + (img.alt || 'image'));
      };
      if (img.complete) { mark(); } else { img.addEventListener('load', mark, { once: true }); }
      img.addEventListener('keydown', function (e) {
        if (!img.classList.contains('content-zoom') || (e.key !== 'Enter' && e.key !== ' ')) { return; }
        e.preventDefault();
        open(img, img.currentSrc || img.src, img, '');
      });
    });
  }
  if (document.readyState === 'loading') { document.addEventListener('DOMContentLoaded', enableImages); } else { enableImages(); }
})();
