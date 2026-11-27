/*
 * SPECTRA docs page behaviour (see _layouts/default.html):
 *  - ATP header (1:1 copy of automatetheplanet.com): the hamburger opens the menu panel (#menu-open / #menu,
 *    as the site's interactive.js does); the docs search icon expands to the field, Escape or leaving an empty
 *    field collapses it (the bellatrix.solutions pattern)
 *  - docs nav (.spectra-aside), from 80em: starts 56px below the header inside the heading band; once the page
 *    scrolls past that it stays 24px below the viewport top (scrolling inside when taller) and parks at the end
 *    of the page; the page is kept at least as tall as the nav
 *  - below 80em the nav is a collapsed "Documentation menu" above the heading band
 */
(function () {
  'use strict';

  // ----- ATP header: mobile menu -----
  var menuOpen = document.getElementById('menu-open');
  var menu = document.getElementById('menu');
  if (menuOpen && menu) {
    menuOpen.setAttribute('role', 'button');
    menuOpen.setAttribute('tabindex', '0');
    menuOpen.setAttribute('aria-label', 'Menu');
    menuOpen.setAttribute('aria-expanded', 'false');
    var toggleMenu = function (e) {
      e.preventDefault();
      var open = menu.classList.toggle('opened');
      menuOpen.classList.toggle('toggled', open);
      menuOpen.setAttribute('aria-expanded', open ? 'true' : 'false');
    };
    menuOpen.addEventListener('click', toggleMenu);
    menuOpen.addEventListener('keydown', function (e) { if (e.key === 'Enter' || e.key === ' ') toggleMenu(e); });
  }

  // ----- ATP header: docs search icon -----
  var searchBtn = document.querySelector('.atp-site-header .menu-search-toggle');
  var searchLi = searchBtn && searchBtn.closest('.docs-search');
  var searchInput = document.getElementById('search-input');
  if (searchBtn && searchLi && searchInput) {
    var setSearch = function (open, refocus) {
      searchLi.classList.toggle('is-open', open);
      searchBtn.setAttribute('aria-expanded', open ? 'true' : 'false');
      if (open) searchInput.focus();
      else if (refocus) searchBtn.focus();
    };
    searchBtn.addEventListener('click', function (e) { e.preventDefault(); setSearch(searchBtn.getAttribute('aria-expanded') !== 'true'); });
    searchLi.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && searchLi.classList.contains('is-open')) { e.preventDefault(); setSearch(false, true); }
    });
    searchLi.addEventListener('focusout', function (e) {
      if (!searchLi.classList.contains('is-open') || searchLi.contains(e.relatedTarget) || searchInput.value.trim()) return;
      setSearch(false, false);
    });
    // "/" focuses the search (and opens the panel or the field first)
    document.addEventListener('keydown', function (e) {
      if (e.key !== '/' || e.ctrlKey || e.metaKey || e.altKey) return;
      var t = e.target;
      if (t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName))) return;
      e.preventDefault();
      if (getComputedStyle(searchBtn).display !== 'none') setSearch(true);
      else {
        if (menu && !menu.classList.contains('opened') && menuOpen) menuOpen.click();
        searchInput.focus();
      }
    });
  }

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
