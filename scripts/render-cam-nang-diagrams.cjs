const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
const puppeteer = require('../frontend/projectmgmt-web/node_modules/puppeteer-core');
const dir = path.resolve(__dirname, '../docs/cam-nang-du-an/diagrams');
const svgDir = path.join(dir, 'svg');
const pngDir = path.join(dir, 'png');
fs.mkdirSync(pngDir, { recursive: true });
const edge = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
(async()=>{
  const browser = await puppeteer.launch({executablePath:edge,headless:true,args:['--no-sandbox','--disable-gpu'],protocolTimeout:120000});
  try {
    for (const name of fs.readdirSync(svgDir).filter(x=>x.endsWith('.svg')).sort()) {
      const source = fs.readFileSync(path.join(svgDir,name),'utf8');
      const size = source.match(/<svg[^>]*width="(\d+)" height="(\d+)"/);
      if(!size)throw Error('missing size '+name);
      const page=await browser.newPage();
      await page.setViewport({width:Number(size[1]),height:Number(size[2]),deviceScaleFactor:1});
      await page.goto(pathToFileURL(path.join(svgDir,name)).href,{waitUntil:'load'});
      await page.screenshot({path:path.join(pngDir,name.replace(/\.svg$/,'.png')),type:'png'});
      await page.close();
      console.log(name);
    }
  } finally {await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
