const fs = require('fs');
const path = require('path');

const summaryPath = path.join(__dirname, 'stitch_rendered_pngs', 'stitch_summary.json');
const data = JSON.parse(fs.readFileSync(summaryPath, 'utf8'));

console.log(`Total screens parsed: ${data.totalScreens}\n`);

data.screenSummaries.forEach((s, idx) => {
  console.log(`=== Screen ${idx + 1}: ${s.screenName} ===`);
  console.log(`Title: ${s.title}`);
  console.log(`Snippet: ${s.textSnippet.substring(0, 300)}...`);
  console.log(`Images: ${s.imgs.length}`);
  s.imgs.forEach(img => console.log(` - Img: ${img.src} (${img.alt})`));
  console.log('--------------------------------------------------\n');
});
