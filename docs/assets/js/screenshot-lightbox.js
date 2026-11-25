// Lightbox for documentation screenshots (_includes/screenshot.html). No library.
// Every screenshot is a link to its full-size image, so without JavaScript the link simply opens the file.
// With JavaScript the link opens a modal <dialog>: Esc, the close button or a click outside closes it, and focus
// returns to the link.
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

  document.addEventListener('click', function (e) {
    var link = e.target.closest && e.target.closest('a.screenshot-link');
    if (!link || e.ctrlKey || e.metaKey || e.shiftKey || e.button !== 0) { return; }
    if (typeof HTMLDialogElement !== 'function') { return; } // old browser: follow the link
    e.preventDefault();
    if (!dialog) { build(); }
    var img = link.querySelector('img');
    var figcaption = link.parentNode.querySelector('figcaption');
    opener = link;
    image.src = link.href;
    image.alt = img ? img.alt : '';
    caption.textContent = figcaption ? figcaption.textContent : (img ? img.alt : '');
    document.documentElement.classList.add('screenshot-lightbox-open');
    dialog.showModal(); // Esc closes a modal dialog natively
  });
})();
