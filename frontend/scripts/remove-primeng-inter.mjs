import { readdir, readFile, rm, writeFile } from "node:fs/promises";
import path from "node:path";

const browserDirectory = path.resolve("dist/frontend/browser");
const files = await readdir(browserDirectory, { recursive: true });
const styleFiles = files.filter((file) => /^styles-.*\.css$/i.test(path.basename(file)));

if (styleFiles.length !== 1) {
  throw new Error(`Expected one generated styles file, found ${styleFiles.length}.`);
}

const stylePath = path.join(browserDirectory, styleFiles[0]);
let css = await readFile(stylePath, "utf8");

css = css
  .replace(/@font-face\s*\{[^{}]*font-family:\s*(?:["']Inter var["']|Inter var);[^{}]*\}\s*/gi, "")
  .replace(/(?:["']Inter var["']|Inter var)/gi, '"Google Sans"');

if (/Inter(?: var|-roman|-italic)/i.test(css)) {
  throw new Error("PrimeNG Inter references remain in the generated stylesheet.");
}

await writeFile(stylePath, css, "utf8");

for (const file of files.filter((file) => /^Inter-.*\.woff2$/i.test(path.basename(file)))) {
  await rm(path.join(browserDirectory, file));
}
