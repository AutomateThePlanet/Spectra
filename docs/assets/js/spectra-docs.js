/*
 * SPECTRA docs page behaviour (see _layouts/default.html):
 *  - docs nav (.spectra-aside), from 80em: starts 56px below the header inside the heading band; once the page
 *    scrolls past that it stays 24px below the viewport top (scrolling inside when taller) and parks at the end
 *    of the page; the page is kept at least as tall as the nav
 *  - below 80em the nav is a collapsed "Documentation menu" above the heading band
 */
(function () {
  'use strict';

  // ----- docs nav -----
  var aside = document.querySelector('.spectra-aside');
  var main = document.querySelector('.spectra-main');
  if (!aside || !main) return;

  var navToggle = aside.querySelector('.spectra-nav-toggle');
  if (navToggle) {
    navToggle.addEventListener('click', function () {
      var open = aside.classList.toggle('is-open');
      navToggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    });
  }

  var wide = window.matchMedia('(min-width: 80em)');
  var GAP = 24, START = 56, ticking = false;
  function reset() {
    aside.classList.remove('is-stuck', 'is-parked');
    aside.style.left = '';
    aside.style.width = '';
  }
  function place() {
    ticking = false;
    if (!wide.matches) { reset(); main.style.minHeight = ''; return; }
    var y = window.scrollY;
    var mainRect = main.getBoundingClientRect();
    var mainTop = mainRect.top + y;
    var natural = mainTop + START;
    var h = aside.offsetHeight;
    main.style.minHeight = (START + h + 48) + 'px';
    var mainBottom = mainTop + main.offsetHeight;
    if (y + GAP <= natural) { reset(); return; }
    if (y + GAP + h >= mainBottom) {
      aside.classList.remove('is-stuck');
      aside.style.left = '';
      aside.style.width = '';
      aside.classList.add('is-parked');
      return;
    }
    if (!aside.classList.contains('is-stuck')) {
      // fix it where it is: same left edge and width as in the flow
      aside.classList.remove('is-parked');
      var r = aside.getBoundingClientRect();
      aside.style.width = r.width + 'px';
      aside.style.left = r.left + 'px';
      aside.classList.add('is-stuck');
    }
  }
  function schedule() { if (!ticking) { ticking = true; window.requestAnimationFrame(place); } }
  window.addEventListener('scroll', schedule, { passive: true });
  window.addEventListener('resize', function () { reset(); schedule(); });
  if (wide.addEventListener) wide.addEventListener('change', function () { reset(); schedule(); });
  window.addEventListener('load', schedule);
  place();
})();
