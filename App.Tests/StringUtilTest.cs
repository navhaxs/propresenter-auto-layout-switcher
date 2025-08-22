using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProPresenter_StageDisplayLayout_AutoSwitcher;

namespace App.Tests;

[TestClass]
[TestSubject(typeof(StringUtil))]
public class StringUtilTest
{

    [TestMethod]
    public void TestLibraryNameExtraction()
    {
        // Unix-style path with forward slashes
        var pathUnix = "/Users/me/ProPresenter/Libraries/My Library/Something/file.pro";
        Assert.AreEqual("My Library", StringUtil.ExtractLibraryNameFromPath(pathUnix), "Should extract library name from forward-slash path");

        Assert.AreEqual("Default", StringUtil.ExtractLibraryNameFromPath("C:\\Users\\me\\Documents\\ProPresenter\\Libraries\\Default\\SERVICE LOOP.pro"), "Should extract library name from backslash path");
        
        // Windows-style path with backslashes
        var pathWindows = @"C:\Users\me\ProPresenter\Libraries\My Library\Something\file.pro";
        Assert.AreEqual("My Library", StringUtil.ExtractLibraryNameFromPath(pathWindows), "Should extract library name from backslash path");

        // Path without Libraries segment should return empty
        var pathNoLib = "/Users/me/ProPresenter/NoLib/abc";
        Assert.AreEqual(string.Empty, StringUtil.ExtractLibraryNameFromPath(pathNoLib), "Should return empty string when Libraries segment is missing");
    }
}