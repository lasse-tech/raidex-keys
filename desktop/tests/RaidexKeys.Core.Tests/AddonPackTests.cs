// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.AddonPack;

namespace RaidexKeys.Core.Tests;

/// <summary>The addon as WoW gets it: no comments, the code untouched, tidy.</summary>
public sealed class AddonPackTests
{
    [Fact]
    public void CommentsGoStringsStay()
    {
        const string lua = """
            --- Doc comment
            local a = "x -- no comment" -- a comment
            local b = 'it''s' .. [[raw -- text]] .. [==[also ]] -- raw]==]
            --[[ block
                 over lines ]] local c = a--[==[ inline ]==]..b
            local d = "esc \" -- still a string" --
            """;
        Assert.Equal("""
            local a = "x -- no comment"
            local b = 'it''s' .. [[raw -- text]] .. [==[also ]] -- raw]==]
             local c = a ..b
            local d = "esc \" -- still a string"

            """, AddonSource.StripLua(lua));

        Assert.Equal("<Ui>\n  <Frame />\n</Ui>\n", AddonSource.StripXml("<Ui>\n<!-- a\ncomment -->\n  <Frame /> <!-- x -->\n</Ui>"));
        Assert.Equal("## Title: X\n## X-Build: 21\nCore.lua",
            AddonSource.StripToc("## Title: X\n# a comment\n## X-Build: dev\nCore.lua", "21"));
    }

    [Fact]
    public void CommentLinesLeaveNoGapsBlankLinesStayOnce()
    {
        const string lua = "-- Header\n-- of the file\n\nlocal a = 1\n-- why b\nlocal b = 2\n\n\n\n-- Section\n\nlocal s = [[\n\nkeep  \n]]\n-- tail\n";
        Assert.Equal("local a = 1\nlocal b = 2\n\nlocal s = [[\n\nkeep  \n]]\n", AddonSource.StripLua(lua));
        Assert.Equal("", AddonSource.StripLua("-- only\n-- comments\n"));
    }

    [Fact]
    public void AFileOfCommentsOnlyIsBlankOnceStripped()
    {
        Assert.True(AddonSource.IsBlank(AddonSource.StripLua("-- nothing yet\n--[[ but\n comments ]]\n")));
        Assert.True(AddonSource.IsBlank(AddonSource.StripXml("<!-- nothing -->\n")));
        Assert.False(AddonSource.IsBlank(AddonSource.StripLua("-- a note\nlocal _, T = ...\n")));
    }
}
