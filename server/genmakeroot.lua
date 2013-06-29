
solution "VooServerRoot"
    dofile('genmake.os.lua')

    flags { "ExtraWarnings" }

    configuration "debug"
        defines "DEBUG"
        flags "Symbols"
        targetdir "bin/debug"
        objectsdir "obj/debug"
    done "debug"

    configuration "release"
        defines "NDEBUG"
        flags "Optimize"
        targetdir "bin/release"
        objectsdir "obj/release"
    done "release"

    include "vooplayer"
    include "vooserver"

done "VooServerRoot"

