set -x
rm -rf vooserver.app
cp -a vooserver/bin/Release/vooserver.app .
cp -a vooplayer/bin/Release/vooplayer.app vooserver.app/Contents/Resources/
du -a vooserver.app
