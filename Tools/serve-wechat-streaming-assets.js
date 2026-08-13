'use strict';

const fs = require('fs');
const http = require('http');
const path = require('path');

const host = process.env.WX_ASSET_HOST || '127.0.0.1';
const port = Number(process.env.WX_ASSET_PORT || 18081);
const miniGameRoot = path.resolve(
    process.env.WX_MINIGAME_ROOT || path.join(__dirname, '..', 'UnityProject', 'Build', 'WeChatMiniGame', 'minigame'),
);
const webglRoot = path.resolve(
    process.env.WX_WEBGL_ROOT || path.join(miniGameRoot, '..', 'webgl'),
);
const yooAssetRoots = process.env.WX_YOO_CDN_ROOT
    ? [path.resolve(process.env.WX_YOO_CDN_ROOT)]
    : [
        path.join(miniGameRoot, 'StreamingAssets', 'yoo'),
        path.join(webglRoot, 'StreamingAssets', 'yoo'),
    ];

function resolveFromRoot(root, relativePath) {
    const filePath = path.resolve(root, relativePath);
    return filePath === root || filePath.startsWith(`${root}${path.sep}`) ? filePath : null;
}

function resolveFile(requestUrl) {
    const pathname = decodeURIComponent(new URL(requestUrl, 'http://localhost').pathname);
    const relativePath = pathname.replace(/^[/\\]+/, '');

    // CDN mode fetches WebGL code/data by filename, while YooAsset uses StreamingAssets paths.
    const roots = [
        ...yooAssetRoots,
        miniGameRoot,
        webglRoot,
        path.join(miniGameRoot, 'wasmcode'),
        path.join(miniGameRoot, 'data-package'),
    ];

    for (const root of roots) {
        const filePath = resolveFromRoot(root, relativePath);
        if (filePath && fs.existsSync(filePath) && !fs.statSync(filePath).isDirectory()) {
            return filePath;
        }
    }

    return null;
}

http.createServer((request, response) => {
    if (request.method !== 'GET' && request.method !== 'HEAD') {
        response.writeHead(405);
        response.end();
        return;
    }

    const filePath = resolveFile(request.url || '/');
    if (!filePath) {
        response.writeHead(404);
        response.end('Not found');
        return;
    }

    response.writeHead(200, {
        'Cache-Control': 'no-store',
        'Content-Type': 'application/octet-stream',
        'Content-Length': fs.statSync(filePath).size,
    });
    if (request.method === 'HEAD') {
        response.end();
        return;
    }

    fs.createReadStream(filePath).pipe(response);
}).listen(port, host, () => {
    console.log(`Serving WeChat assets from ${miniGameRoot}, ${webglRoot}, and ${yooAssetRoots.join(', ')} at http://${host}:${port}`);
});
