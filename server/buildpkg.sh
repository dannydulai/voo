set -x
rm -rf vooserver.app
cp -a vooserver/bin/Release/vooserver.app .
cp -a vooplayer/bin/Release/vooplayer.app vooserver.app/Contents/Resources/
du -a vooserver.app
echo exit 0 > vooserver.app/Contents/Resources/vooplayer.app/Contents/MacOS/mono-version-check
chmod +x vooserver.app/Contents/Resources/vooplayer.app/Contents/MacOS/mono-version-check
