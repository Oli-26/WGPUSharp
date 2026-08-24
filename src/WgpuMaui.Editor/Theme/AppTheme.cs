namespace WgpuMaui.Editor.Theme;

public static class AppTheme
{
     public static MudTheme RoyalBlueTheme => new()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = "#93C5FD",
            PrimaryLighten = "#BFDBFE",
            PrimaryDarken = "#60A5FA",
            PrimaryContrastText = "#0f172a",

            Secondary = "#BFDBFE",
            SecondaryLighten = "#DBEAFE",
            SecondaryDarken = "#93C5FD",
            SecondaryContrastText = "#0f172a",

            Tertiary = "#60A5FA",
            TertiaryLighten = "#93C5FD",
            TertiaryDarken = "#3B82F6",
            TertiaryContrastText = "#FFFFFF",

            Background = "#0f172a",
            BackgroundGray = "#1e293b",
            Surface = "rgba(255,255,255,0.05)",

            AppbarBackground = "#0f172a",
            AppbarText = "#E5E7EB",

            DrawerBackground = "#020617",
            DrawerText = "#E5E7EB",
            DrawerIcon = "#94A3B8",

            TextPrimary = "#E5E7EB",
            TextSecondary = "#94A3B8",
            TextDisabled = "#64748B",

            Success = "#86EFAC",
            SuccessLighten = "#BBF7D0",
            SuccessDarken = "#4ADE80",
            SuccessContrastText = "#022c22",

            Warning = "#FDE68A",
            WarningLighten = "#FEF3C7",
            WarningDarken = "#FACC15",
            WarningContrastText = "#422006",

            Error = "#FCA5A5",
            ErrorLighten = "#FECACA",
            ErrorDarken = "#EF4444",
            ErrorContrastText = "#450a0a",

            Info = "#60A5FA",
            InfoLighten = "#93C5FD",
            InfoDarken = "#3B82F6",
            InfoContrastText = "#083344",

            Divider = "rgba(148,163,184,0.2)",
            DividerLight = "rgba(148,163,184,0.1)",
            TableStriped = "rgba(255,255,255,0.02)",
            TableHover = "rgba(147,197,253,0.08)",
            TableLines = "rgba(148,163,184,0.2)",

            ActionDefault = "#CBD5F5",
            ActionDisabled = "#475569",
            ActionDisabledBackground = "rgba(255,255,255,0.05)",
            HoverOpacity = 0.06,
            RippleOpacity = 0.1,
            RippleOpacitySecondary = 0.08,
            BorderOpacity = 0.12,

            Black = "#000000",
            White = "#FFFFFF",
            Dark = "#020617",
            DarkLighten = "#1e293b",
            DarkDarken = "#020617",
            DarkContrastText = "#E5E7EB"
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#60A5FA",
            PrimaryLighten = "#93C5FD",
            PrimaryDarken = "#3B82F6",
            PrimaryContrastText = "#020617",

            Secondary = "#93C5FD",
            SecondaryLighten = "#BFDBFE",
            SecondaryDarken = "#60A5FA",
            SecondaryContrastText = "#020617",

            Tertiary = "#BFDBFE",
            TertiaryLighten = "#E0F2FE",
            TertiaryDarken = "#7DD3FC",
            TertiaryContrastText = "#020617",

            Background = "#020617",        
            BackgroundGray = "#0f172a",    
            Surface = "#1e293b",           

            AppbarBackground = "#020617",
            AppbarText = "#E5E7EB",

            DrawerBackground = "#020617",
            DrawerText = "#E5E7EB",
            DrawerIcon = "#60A5FA",

            TextPrimary = "#F9FAFB",
            TextSecondary = "#94A3B8",
            TextDisabled = "#475569",

            Success = "#4ADE80",
            SuccessLighten = "#86EFAC",
            SuccessDarken = "#22C55E",
            SuccessContrastText = "#022c22",

            Warning = "#FACC15",
            WarningLighten = "#FDE68A",
            WarningDarken = "#EAB308",
            WarningContrastText = "#422006",

            Error = "#F87171",
            ErrorLighten = "#FCA5A5",
            ErrorDarken = "#EF4444",
            ErrorContrastText = "#450a0a",

            Info = "#38BDF8",
            InfoLighten = "#7DD3FC",
            InfoDarken = "#0284C7",
            InfoContrastText = "#083344",

            Divider = "rgba(148,163,184,0.2)",
            DividerLight = "rgba(148,163,184,0.1)",
            TableStriped = "rgba(255,255,255,0.03)",
            TableHover = "rgba(96, 165, 250, 0.1)",
            TableLines = "rgba(148,163,184,0.2)",

            ActionDefault = "#CBD5F5",
            ActionDisabled = "#475569",
            ActionDisabledBackground = "rgba(255,255,255,0.05)",

            HoverOpacity = 0.08,
            RippleOpacity = 0.1,
            RippleOpacitySecondary = 0.08,
            BorderOpacity = 0.12,

            Black = "#000000",
            White = "#FFFFFF",
            Dark = "#020617",
            DarkLighten = "#1e293b",
            DarkDarken = "#020617",
            DarkContrastText = "#E5E7EB"
        },
        LayoutProperties = new LayoutProperties() { DefaultBorderRadius = "28px", AppbarHeight = "30px" },
    };
    public static MudTheme SkyBlueTheme => new()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = "#0284C7",          
            PrimaryLighten = "#7DD3FC",
            PrimaryDarken = "#0369A1",
            PrimaryContrastText = "#FFFFFF",

            Secondary = "#0EA5E9",        
            SecondaryLighten = "#BAE6FD",
            SecondaryDarken = "#0284C7",
            SecondaryContrastText = "#FFFFFF",

            Tertiary = "#38BDF8",
            TertiaryLighten = "#E0F2FE",
            TertiaryDarken = "#075985",
            TertiaryContrastText = "#FFFFFF",

            Background = "#F8FAFC",       
            BackgroundGray = "#F1F5F9",   
            Surface = "#F0F0F0",          

            AppbarBackground = "#0284C7",
            AppbarText = "#FFFFFF",

            DrawerBackground = "#F8FAFC",
            DrawerText = "#1E293B",
            DrawerIcon = "#0284C7",

            TextPrimary = "#0F172A",      
            TextSecondary = "#475569",
            TextDisabled = "#94A3B8",

            Success = "#16A34A",
            SuccessLighten = "#BBF7D0",
            SuccessDarken = "#15803D",
            SuccessContrastText = "#FFFFFF",

            Warning = "#D97706",
            WarningLighten = "#FEF3C7",
            WarningDarken = "#B45309",
            WarningContrastText = "#FFFFFF",

            Error = "#DC2626",
            ErrorLighten = "#FECACA",
            ErrorDarken = "#B91C1C",
            ErrorContrastText = "#FFFFFF",

            Info = "#0891B2",
            InfoLighten = "#CFFAFE",
            InfoDarken = "#155E75",
            InfoContrastText = "#FFFFFF",

            Divider = "rgba(14, 165, 233, 0.2)",
            DividerLight = "rgba(14, 165, 233, 0.1)",
            TableStriped = "rgba(14, 165, 233, 0.03)",
            TableHover = "rgba(14, 165, 233, 0.08)",
            TableLines = "rgba(14, 165, 233, 0.2)",

            ActionDefault = "#475569",
            ActionDisabled = "#CBD5E1",
            ActionDisabledBackground = "rgba(0, 0, 0, 0.05)",
            
            HoverOpacity = 0.06,
            RippleOpacity = 0.1,
            RippleOpacitySecondary = 0.08,
            BorderOpacity = 0.12,

            Black = "#000000",
            White = "#FFFFFF",
            Dark = "#1E293B",
            DarkLighten = "#475569",
            DarkDarken = "#0F172A",
            DarkContrastText = "#FFFFFF"
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#38BDF8",          // Azul claro vibrante
            PrimaryLighten = "#7DD3FC",
            PrimaryDarken = "#0284C7",
            PrimaryContrastText = "#020617",

            Secondary = "#0284C7",
            SecondaryLighten = "#38BDF8",
            SecondaryDarken = "#0369A1",
            SecondaryContrastText = "#FFFFFF",

            Tertiary = "#60A5FA",
            TertiaryLighten = "#93C5FD",
            TertiaryDarken = "#2563EB",
            TertiaryContrastText = "#FFFFFF",

            Background = "#0F172A",       // Azul noturno profundo
            BackgroundGray = "#1E293B",
            Surface = "#1E293B",

            AppbarBackground = "#0F172A",
            AppbarText = "#F8FAFC",

            DrawerBackground = "#0F172A",
            DrawerText = "#F8FAFC",
            DrawerIcon = "#38BDF8",

            TextPrimary = "#F8FAFC",
            TextSecondary = "#94A3B8",
            TextDisabled = "#475569",

            Success = "#4ADE80",
            SuccessLighten = "#86EFAC",
            SuccessDarken = "#16A34A",
            SuccessContrastText = "#052e16",

            Warning = "#FACC15",
            WarningLighten = "#FEF3C7",
            WarningDarken = "#D97706",
            WarningContrastText = "#422006",

            Error = "#F87171",
            ErrorLighten = "#FECACA",
            ErrorDarken = "#B91C1C",
            ErrorContrastText = "#FFFFFF",

            Info = "#22D3EE",
            InfoLighten = "#A5F3FC",
            InfoDarken = "#0891B2",
            InfoContrastText = "#020617",

            Divider = "rgba(56, 189, 248, 0.2)",
            DividerLight = "rgba(56, 189, 248, 0.1)",
            TableStriped = "rgba(255, 255, 255, 0.03)",
            TableHover = "rgba(56, 189, 248, 0.1)",
            TableLines = "rgba(56, 189, 248, 0.2)",

            ActionDefault = "#94A3B8",
            ActionDisabled = "#475569",
            ActionDisabledBackground = "rgba(255, 255, 255, 0.05)",

            HoverOpacity = 0.08,
            RippleOpacity = 0.1,
            RippleOpacitySecondary = 0.08,
            BorderOpacity = 0.12,

            Black = "#000000",
            White = "#FFFFFF",
            Dark = "#020617",
            DarkLighten = "#1E293B",
            DarkDarken = "#000000",
            DarkContrastText = "#F8FAFC"
        },
        LayoutProperties = new LayoutProperties() { DefaultBorderRadius = "28px", AppbarHeight = "30px" },
    };
}
