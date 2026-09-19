const fs = require('fs');
const path = require('path');

const downloadsDir = 'C:\\Users\\hoang\\Downloads';
const files = fs.readdirSync(downloadsDir).map(f => {
  const p = path.join(downloadsDir, f);
  try {
    const stat = fs.statSync(p);
    return { name: f, path: p, mtime: stat.mtimeMs, size: stat.size, isFile: stat.isFile() };
  } catch (e) {
    return null;
  }
}).filter(f => f && f.isFile);

const imageExts = ['.jpg', '.jpeg', '.png', '.webp', '.svg'];
const images = files.filter(f => imageExts.includes(path.extname(f.name).toLowerCase()));
images.sort((a, b) => b.mtime - a.mtime);

console.log('Top 20 image files in C:\\Users\\hoang\\Downloads:');
images.slice(0, 20).forEach(img => {
  console.log(`${img.name} | MTIME: ${new Date(img.mtime).toISOString()} | SIZE: ${img.size} bytes`);
});
