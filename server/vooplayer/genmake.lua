project "vooplayer"
    kind "WindowedApp"
    language "C#"

    flags { "Unsafe" }

    linksystemlibs {
        "System",
    }

    compilefiles {
        "AppDelegate.cs",
        "AppDelegate.designer.cs",
        "Main.cs",
        "vlc.cs",
        
        "MainMenu.xib",
    }
    
    infoplist "Info.plist"

    linkfiles { "MonoMac" }

done "vooplayer"
