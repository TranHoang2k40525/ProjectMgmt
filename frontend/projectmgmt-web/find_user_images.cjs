const fs = require('fs');
const path = require('path');

const dir = 'C:\\Users\\hoang\\.gemini\\antigravity\\brain\\89b0efb9-4947-4929-87b6-eadb839672a9\\.tempmediaStorage';
const files = fs.readdirSync(dir).map(f => {
  const p = path.join(dir, f);
  const stat = fs.statSync(p);
  return { name: f, path: p, mtime: stat.mtimeMs, size: stat.size };
});

files.sort((a, b) => b.mtime - a.mtime);

console.log('Recent media files:');
files.slice(0, 10).forEach(f => {
  console.log(`${f.name} - ${new Date(f.mtime).toISOString()} - ${f.size} bytes`);
});
