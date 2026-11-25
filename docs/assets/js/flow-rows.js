// Flow diagrams (_includes/flow.html) wrap onto several rows. The dashed arrow after a step points to the next step
// on the same row; after the last step of a row there is nothing to point at. This marks those steps with
// .spectra-step-row-end (CSS hides their arrow), on load and whenever the layout width changes.
(function () {
  'use strict';
  function mark() {
    document.querySelectorAll('.spectra-steps').forEach(function (list) {
      var items = list.children;
      for (var i = 0; i < items.length; i++) {
        var next = items[i + 1];
        var rowEnd = !next || next.offsetTop > items[i].offsetTop + 4;
        items[i].classList.toggle('spectra-step-row-end', rowEnd);
      }
    });
  }
  var timer;
  function schedule() { clearTimeout(timer); timer = setTimeout(mark, 60); }
  document.addEventListener('DOMContentLoaded', mark);
  window.addEventListener('load', mark);
  window.addEventListener('resize', schedule);
})();
