using System;
using Microsoft.Win32;
using WinToggle;

public static class SelfTest
{
    static int failures = 0;

    static void Check(string name, bool condition)
    {
        if (condition)
        {
            Console.WriteLine("PASS  " + name);
        }
        else
        {
            Console.WriteLine("FAIL  " + name);
            failures++;
        }
    }

    public static int Main()
    {
        string root = "Software\\WinToggleSelfTest";
        try { Registry.CurrentUser.DeleteSubKeyTree(root, false); }
        catch { }

        // DWORD
        Setting d = new Setting("t", "t", "Test", root, "DwordVal", 1, 0, false, RegKind.Dword);
        Check("dword default off", !Reg.IsOn(d));
        Reg.Set(d, true);
        Check("dword write on", Reg.IsOn(d));
        Reg.Set(d, false);
        Check("dword write off", !Reg.IsOn(d));

        // STRING
        Setting s = new Setting("t", "t", "Test", root, "StringVal", "1", "0", true, RegKind.String);
        Check("string default on", Reg.IsOn(s));
        Reg.Set(s, false);
        Check("string write off", !Reg.IsOn(s));
        Reg.Set(s, true);
        Check("string write on", Reg.IsOn(s));

        // KEY EXISTS
        Setting k = new Setting("t", "t", "Test", root + "\\KeyExists", "", "", null, false, RegKind.KeyExists);
        Check("keyexists default off", !Reg.IsOn(k));
        Reg.Set(k, true);
        Check("keyexists write on", Reg.IsOn(k));
        Reg.Set(k, false);
        Check("keyexists write off", !Reg.IsOn(k));

        try { Registry.CurrentUser.DeleteSubKeyTree(root, false); }
        catch { }

        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : failures + " TEST(S) FAILED");
        return failures;
    }
}
