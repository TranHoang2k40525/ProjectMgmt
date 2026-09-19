const fs = require('fs');
const path = require('path');

const baseDir = 'C:\\Users\\hoang\\.gemini\\antigravity\\brain\\89b0efb9-4947-4929-87b6-eadb839672a9';

function walk(dir) {
  let results = [];
  const list = fs.readdirSync(dir);
  list.forEach(file => {
    const fullPath = path.join(dir, file);
    const stat = fs.statSync(fullPath);
    if (stat && stat.isDirectory()) {
      if (!fullPath.includes('.system_generated')) {
        results = results.concat(walk(fullPath));
      }
    } else {
      if (file.endsWith('.png') || file.endsWith('.jpg') || file.endsWith('.jpeg') || file.endsWith('.webp')) {
        results.push({ path: fullPath, mtime: stat.mtimeMs, size: stat.size, file });
      }
    }
  });
  return results;
}

const allImages = walk(baseDir);
allImages.sort((a, b) => b.mtime - a.mtime);

console.log('Top 15 newest image files in brain dir:');
allImages.slice(0, 15).forEach(img => {
  console.log(`${img.file} | MTIME: ${new Date(img.mtime).toISOString()} | SIZE: ${img.size} | PATH: ${img.path}`);
});
