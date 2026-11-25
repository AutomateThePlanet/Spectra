// Image helpers for the documentation screenshots (used by capture.js).
//
// Every image is captured at deviceScaleFactor 2 and saved as WebP of at most 200 KB. Its display size
// (half the pixel size, in CSS px) is recorded in docs/_data/screenshots.yml, which _includes/screenshot.html
// reads for the width and height attributes, so a page never reserves the wrong space and never upscales.
//
// Re-crop a raw PNG capture (for example a live progress-page capture that can't be replayed):
//   node images.js crop <in.png> <name>          -> docs/assets/images/screenshots/<name>.webp

const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const OUT = path.resolve(__dirname, '..', '..', 'assets', 'images', 'screenshots');
const DATA = path.resolve(__dirname, '..', '..', '_data', 'screenshots.yml');
const SCALE = 2;          // deviceScaleFactor of every capture
const MAX_BYTES = 200 * 1024;
const MAX_WIDTH = 2000;   // px; 1000 CSS px at 2x

/** Records the CSS-pixel size of one image in docs/_data/screenshots.yml (sorted, one line per image). */
function recordSize(name, width, height) {
  const sizes = {};
  if (fs.existsSync(DATA)) {
    for (const line of fs.readFileSync(DATA, 'utf8').split(/\r?\n/)) { // CRLF checkouts too
      const m = line.match(/^([\w-]+): \{ width: (\d+), height: (\d+) \}$/);
      if (m) sizes[m[1]] = [+m[2], +m[3]];
    }
  }
  sizes[name] = [Math.round(width / SCALE), Math.round(height / SCALE)];
  const body = Object.keys(sizes).sort().map(k => `${k}: { width: ${sizes[k][0]}, height: ${sizes[k][1]} }`).join('\n');
  fs.mkdirSync(path.dirname(DATA), { recursive: true });
  fs.writeFileSync(DATA, '# Display size (CSS px) of each screenshot. Written by docs/scripts/docs-screenshots/images.js.\n' + body + '\n');
}

/** Saves a PNG buffer as WebP of at most 200 KB: lowers the quality first, the width only as a last resort. */
async function saveWebp(png, name, outDir = OUT) {
  fs.mkdirSync(outDir, { recursive: true });
  let width = Math.min((await sharp(png).metadata()).width, MAX_WIDTH);
  for (let quality = 86; ; quality -= 6) {
    const { data, info } = await sharp(png).resize({ width, withoutEnlargement: true }).webp({ quality }).toBuffer({ resolveWithObject: true });
    if (data.length <= MAX_BYTES || (quality <= 50 && width <= 1000)) {
      fs.writeFileSync(path.join(outDir, name + '.webp'), data);
      if (outDir === OUT) recordSize(name, info.width, info.height);
      console.log(`${name}.webp  ${info.width}x${info.height}  ${Math.round(data.length / 1024)} KB`);
      return;
    }
    if (quality <= 50) { quality = 92; width = Math.round(width * 0.9); }
  }
}

/**
 * Crops a page capture to its content: drops the dark title bar at the top, then trims the page background
 * around the cards and keeps a small margin. For SPECTRA's progress pages (.spectra-progress.html and
 * .spectra/seam-progress.html), whose cards sit in a wide empty page.
 */
async function cropToContent(png, margin = 48) {
  const img = sharp(png);
  const { width, height } = await img.metadata();
  const { data, info } = await img.clone().raw().toBuffer({ resolveWithObject: true });
  const lum = (x, y) => { const i = (y * info.width + x) * info.channels; return 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2]; };
  let top = 0;
  while (top < height - 1 && lum(4, top) < 160) top++;           // skip the dark header band
  if (top > 0) top = Math.min(height - 1, top + 24);               // and its drop shadow
  const body = await sharp(png).extract({ left: 0, top, width, height: height - top }).toBuffer();
  const bg = await sharp(body).extract({ left: 2, top: 8, width: 1, height: 1 }).raw().toBuffer();
  const { info: t } = await sharp(body).trim({ background: { r: bg[0], g: bg[1], b: bg[2] }, threshold: 12 }).toBuffer({ resolveWithObject: true });
  const left = Math.max(0, -t.trimOffsetLeft - margin);
  const y = Math.max(0, -t.trimOffsetTop - margin);
  const w = Math.min(width - left, t.width + 2 * margin);
  const h = Math.min(height - top - y, t.height + 2 * margin);
  return sharp(body).extract({ left, top: y, width: w, height: h }).png().toBuffer();
}

module.exports = { saveWebp, cropToContent, recordSize, OUT, SCALE };

if (require.main === module && process.argv[2] === 'crop') {
  const [, , , input, name] = process.argv;
  (async () => saveWebp(await cropToContent(fs.readFileSync(input)), name))();
}
