#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

const binDir = path.join(__dirname, '..', 'bin');
const binaryName = process.platform === 'win32' ? 'hookreplay.exe' : 'hookreplay';
const binaryPath = path.join(binDir, binaryName);

if (fs.existsSync(binaryPath)) {
  fs.unlinkSync(binaryPath);
  console.log('HookReplay CLI uninstalled.');
}
