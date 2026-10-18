const fs = require('fs');
const path = require('path');

const projectRoot = 'C:\\Users\\hoang\\Downloads\\ProjectMgmt\\frontend\\projectmgmt-web';

// 1. Create public/assets/images/huce-branding
const publicBrandingDir = path.join(projectRoot, 'public', 'assets', 'images', 'huce-branding');
if (!fs.existsSync(publicBrandingDir)) {
  fs.mkdirSync(publicBrandingDir, { recursive: true });
}

const srcBrandingDir = path.join(projectRoot, 'src', 'assets', 'images', 'huce-branding');

if (fs.existsSync(srcBrandingDir)) {
  const files = fs.readdirSync(srcBrandingDir);
  for (const f of files) {
    const srcFile = path.join(srcBrandingDir, f);
    const destFile = path.join(publicBrandingDir, f);
    fs.copyFileSync(srcFile, destFile);
    console.log(`Copied ${f} to public/assets/images/huce-branding/`);
  }
}

// 2. Also copy directly to public/images/huce-branding/ for fallback
const publicImagesDir = path.join(projectRoot, 'public', 'images', 'huce-branding');
if (!fs.existsSync(publicImagesDir)) {
  fs.mkdirSync(publicImagesDir, { recursive: true });
}
if (fs.existsSync(srcBrandingDir)) {
  const files = fs.readdirSync(srcBrandingDir);
  for (const f of files) {
    const srcFile = path.join(srcBrandingDir, f);
    const destFile = path.join(publicImagesDir, f);
    fs.copyFileSync(srcFile, destFile);
  }
}

// 3. Update angular.json
const angularJsonPath = path.join(projectRoot, 'angular.json');
const angularJson = JSON.parse(fs.readFileSync(angularJsonPath, 'utf8'));

const assetsArr = angularJson.projects['projectmgmt-web'].architect.build.options.assets;
const hasSrcAssets = assetsArr.some(a => typeof a === 'object' && a.input === 'src/assets');

if (!hasSrcAssets) {
  assetsArr.push({
    "glob": "**/*",
    "input": "src/assets",
    "output": "assets"
  });
  fs.writeFileSync(angularJsonPath, JSON.stringify(angularJson, null, 2));
  console.log('Updated angular.json to include src/assets in build options!');
}
