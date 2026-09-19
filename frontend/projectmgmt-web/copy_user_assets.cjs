const fs = require('fs');
const path = require('path');

const srcBg = 'C:\\Users\\hoang\\.gemini\\antigravity\\brain\\89b0efb9-4947-4929-87b6-eadb839672a9\\media__1789849094144.jpg';
const srcLogo = 'C:\\Users\\hoang\\.gemini\\antigravity\\brain\\89b0efb9-4947-4929-87b6-eadb839672a9\\media__1789849098852.png';

const targetDir = 'C:\\Users\\hoang\\Downloads\\ProjectMgmt\\frontend\\projectmgmt-web\\src\\assets\\images';
if (!fs.existsSync(targetDir)) fs.mkdirSync(targetDir, { recursive: true });

const destBg = path.join(targetDir, 'huce_bg.jpg');
const destLogo = path.join(targetDir, 'huce_logo.png');

fs.copyFileSync(srcBg, destBg);
fs.copyFileSync(srcLogo, destLogo);

console.log('Successfully copied user images to Angular assets:');
console.log(' - huce_bg.jpg:', destBg);
console.log(' - huce_logo.png:', destLogo);
