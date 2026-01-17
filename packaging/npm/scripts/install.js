#!/usr/bin/env node

const https = require('https');
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const VERSION = require('../package.json').version;
const GITHUB_RELEASE_URL = 'https://github.com/ahmedmandur/hookreplay-cli/releases/download';

// Platform mapping
const PLATFORMS = {
  'darwin-x64': 'osx-x64',
  'darwin-arm64': 'osx-arm64',
  'linux-x64': 'linux-x64',
  'linux-arm64': 'linux-arm64',
  'win32-x64': 'win-x64',
  'win32-arm64': 'win-arm64'
};

function getPlatformKey() {
  const platform = process.platform;
  const arch = process.arch;
  return `${platform}-${arch}`;
}

function getBinaryName() {
  return process.platform === 'win32' ? 'hookreplay-bin.exe' : 'hookreplay-bin';
}

function getDownloadUrl(platformKey) {
  const rid = PLATFORMS[platformKey];
  if (!rid) {
    throw new Error(`Unsupported platform: ${platformKey}`);
  }

  const ext = process.platform === 'win32' ? 'zip' : 'tar.gz';
  // Download from GitHub releases
  return `${GITHUB_RELEASE_URL}/v${VERSION}/hookreplay-${VERSION}-${rid}.${ext}`;
}

function downloadFile(url) {
  return new Promise((resolve, reject) => {
    const handleResponse = (response) => {
      // Handle redirects
      if (response.statusCode === 302 || response.statusCode === 301) {
        https.get(response.headers.location, handleResponse).on('error', reject);
        return;
      }

      if (response.statusCode !== 200) {
        reject(new Error(`Failed to download: HTTP ${response.statusCode}`));
        return;
      }

      const chunks = [];
      response.on('data', chunk => chunks.push(chunk));
      response.on('end', () => resolve(Buffer.concat(chunks)));
      response.on('error', reject);
    };

    https.get(url, handleResponse).on('error', reject);
  });
}

async function install() {
  const platformKey = getPlatformKey();
  const binDir = path.join(__dirname, '..', 'bin');
  const binaryName = getBinaryName();
  const binaryPath = path.join(binDir, binaryName);

  console.log(`Installing HookReplay CLI v${VERSION} for ${platformKey}...`);

  // Check if binary already exists
  if (fs.existsSync(binaryPath)) {
    console.log('Binary already exists, skipping download.');
    return;
  }

  try {
    const url = getDownloadUrl(platformKey);
    console.log(`Downloading from: ${url}`);

    const buffer = await downloadFile(url);
    console.log('Download complete. Extracting...');

    // Ensure bin directory exists
    if (!fs.existsSync(binDir)) {
      fs.mkdirSync(binDir, { recursive: true });
    }

    // Extract based on platform
    if (process.platform === 'win32') {
      // For Windows ZIP files
      const AdmZip = require('adm-zip');
      const zip = new AdmZip(buffer);
      zip.extractAllTo(binDir, true);
      // Rename extracted file to hookreplay-bin.exe
      const extractedPath = path.join(binDir, 'hookreplay.exe');
      if (fs.existsSync(extractedPath)) {
        fs.renameSync(extractedPath, path.join(binDir, binaryName));
      }
    } else {
      // For tar.gz files
      const gunzip = zlib.gunzipSync(buffer);

      // Simple tar extraction (single file)
      let offset = 0;
      while (offset < gunzip.length) {
        const header = gunzip.slice(offset, offset + 512);
        if (header[0] === 0) break;

        const fileName = header.slice(0, 100).toString('utf8').replace(/\0/g, '').trim();
        const fileSize = parseInt(header.slice(124, 136).toString('utf8').trim(), 8);

        if (fileName && fileSize > 0) {
          const content = gunzip.slice(offset + 512, offset + 512 + fileSize);
          const destPath = path.join(binDir, binaryName);
          fs.writeFileSync(destPath, content);

          // Make executable on Unix
          fs.chmodSync(destPath, 0o755);
        }

        offset += 512 + Math.ceil(fileSize / 512) * 512;
      }
    }

    console.log('HookReplay CLI installed successfully!');
    console.log(`Run 'hookreplay' to get started.`);

  } catch (error) {
    console.error('Installation failed:', error.message);
    console.error('');
    console.error('Alternative installation methods:');
    console.error('  - dotnet tool install --global HookReplay.Cli');
    console.error('  - Visit https://hookreplay.dev for more options');
    process.exit(1);
  }
}

install();
