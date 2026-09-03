// Regenerates the app icon assets from jules.svg.
//
// Requires the `sharp` package:  npm i -g sharp   (or run via `npx --package sharp node tools/generate-icons.mjs`)
// Usage:                         node tools/generate-icons.mjs
//
// Produces:
//   Assets/Square44x44Logo.png, Square150x150Logo.png, Wide310x150Logo.png,
//   SplashScreen.png, StoreLogo.png, LockScreenLogo.png   (MSIX package assets)
//   Assets/jules.ico                                        (window / taskbar icon)

import sharp from "sharp";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const assets = join(repoRoot, "Assets");
mkdirSync(assets, { recursive: true });

const BG = "#4F7CFF";       // brand blue plate
const FG = "#FFFFFF";       // glyph colour
const GLYPH_W = 84, GLYPH_H = 95; // source viewBox

const rawSvg = readFileSync(join(repoRoot, "jules.svg"), "utf8");
// Recolour the glyph (source uses fill="currentColor") and strip Angular junk attrs.
const glyphSvg = rawSvg
  .replace(/currentColor/g, FG)
  .replace(/_ngcontent-[^=]+="[^"]*"/g, "");

// Rasterise the glyph once, large, then downscale per target.
async function glyphPng(px) {
  return sharp(Buffer.from(glyphSvg), { density: 384 })
    .resize({ width: Math.round(px), height: Math.round(px * GLYPH_H / GLYPH_W), fit: "contain",
              background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .png()
    .toBuffer();
}

function plateSvg(w, h, radius) {
  return Buffer.from(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}">` +
    `<rect width="${w}" height="${h}" rx="${radius}" ry="${radius}" fill="${BG}"/></svg>`
  );
}

// One square/rect asset: blue plate + centred white glyph at `scale` of the shorter side.
async function makeAsset(outPath, w, h, { scale = 0.6, radius = 0 } = {}) {
  const short = Math.min(w, h);
  const gW = Math.round(short * scale);
  const gH = Math.round(gW * GLYPH_H / GLYPH_W);
  const glyph = await glyphPng(gW);
  const buf = await sharp(plateSvg(w, h, radius))
    .composite([{ input: glyph, left: Math.round((w - gW) / 2), top: Math.round((h - gH) / 2) }])
    .png()
    .toBuffer();
  writeFileSync(outPath, buf);
  console.log(`  ${outPath.replace(repoRoot + "\\", "").replace(repoRoot + "/", "")}  ${w}x${h}`);
}

// Build a Windows .ico from PNG-encoded frames (supported Vista+).
async function makeIco(outPath, sizes) {
  const frames = [];
  for (const s of sizes) {
    const glyph = await glyphPng(Math.round(s * 0.62));
    const gW = Math.round(s * 0.62);
    const gH = Math.round(gW * GLYPH_H / GLYPH_W);
    const png = await sharp(plateSvg(s, s, Math.round(s * 0.16)))
      .composite([{ input: glyph, left: Math.round((s - gW) / 2), top: Math.round((s - gH) / 2) }])
      .png().toBuffer();
    frames.push({ size: s, png });
  }
  const header = Buffer.alloc(6);
  header.writeUInt16LE(0, 0);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(frames.length, 4);
  const dir = Buffer.alloc(16 * frames.length);
  let offset = 6 + dir.length;
  frames.forEach((f, i) => {
    const b = i * 16;
    dir.writeUInt8(f.size >= 256 ? 0 : f.size, b + 0);
    dir.writeUInt8(f.size >= 256 ? 0 : f.size, b + 1);
    dir.writeUInt8(0, b + 2);
    dir.writeUInt8(0, b + 3);
    dir.writeUInt16LE(1, b + 4);
    dir.writeUInt16LE(32, b + 6);
    dir.writeUInt32LE(f.png.length, b + 8);
    dir.writeUInt32LE(offset, b + 12);
    offset += f.png.length;
  });
  writeFileSync(outPath, Buffer.concat([header, dir, ...frames.map(f => f.png)]));
  console.log(`  ${outPath.replace(repoRoot + "\\", "").replace(repoRoot + "/", "")}  [${sizes.join(", ")}]`);
}

console.log("Generating icon assets from jules.svg:");
await makeAsset(join(assets, "Square44x44Logo.png"), 44, 44, { scale: 0.66 });
await makeAsset(join(assets, "Square150x150Logo.png"), 150, 150, { scale: 0.58 });
await makeAsset(join(assets, "Wide310x150Logo.png"), 310, 150, { scale: 0.8 });
await makeAsset(join(assets, "SplashScreen.png"), 620, 300, { scale: 0.55 });
await makeAsset(join(assets, "StoreLogo.png"), 50, 50, { scale: 0.66 });
await makeAsset(join(assets, "LockScreenLogo.png"), 24, 24, { scale: 0.72 });
await makeIco(join(assets, "jules.ico"), [16, 24, 32, 48, 64, 128, 256]);
console.log("Done.");
