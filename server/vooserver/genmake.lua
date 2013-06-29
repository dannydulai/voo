project "vooserver"
    kind "ConsoleApp"
    language "C#"

    flags { "Unsafe" }

    linksystemlibs {
        "System",
    }

    compilefiles {
        "comms.cs",
        "server.cs",
    }
done "vooserver"
