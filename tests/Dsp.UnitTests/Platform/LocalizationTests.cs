using System.Text.Json;
using Dsp.EServices.Domain.Catalog;
using Dsp.Platform.Domain;
using Dsp.SharedKernel;

namespace Dsp.UnitTests.Platform;

public class LanguageTests
{
    private static Language Make(string code, bool enabled = true)
    {
        var l = Language.Create(code, TestText.Text(code), "ltr", 0, "test");
        if (enabled) l.Enable("test");
        return l;
    }

    [Fact]
    public void Exactly_one_language_is_the_default_and_it_cannot_be_turned_off_or_deleted()
    {
        var en = Make("en");
        var ar = Make("ar");
        LanguagePolicy.SetDefault([en, ar], en, "test");
        LanguagePolicy.SetDefault([en, ar], ar, "test");

        Assert.False(en.IsDefault);
        Assert.True(ar.IsDefault);
        Assert.Throws<DomainException>(() => ar.Disable("test"));
        Assert.Throws<DomainException>(() => LanguagePolicy.EnsureCanDelete(ar));
        en.Disable("test");
        Assert.False(en.IsEnabled);
    }

    [Theory]
    [InlineData("fr", true)]
    [InlineData("zh-Hans", true)]
    [InlineData("Arabic", false)]
    [InlineData("", false)]
    public void Language_codes_are_bcp47(string code, bool valid)
    {
        if (valid) Assert.Equal(code, Make(code).Code);
        else Assert.Throws<DomainException>(() => Make(code));
    }

    [Fact]
    public void A_language_name_is_itself_multilingual()
    {
        var ur = Language.Create("ur", new LocalizedText([new("en", "Urdu"), new("ar", "الأردية"), new("ur", "اردو")]), "rtl", 2, "test");
        Assert.Equal("اردو", ur.Name["ur"]);
        Assert.Equal("Urdu", ur.Name.Get("fr", "en"));
    }
}

public class LocalizedTextTests
{
    [Fact]
    public void Missing_languages_fall_back_to_the_default_then_to_any_text()
    {
        var text = new LocalizedText([new("en", "Fee"), new("ar", "الرسوم")]);
        Assert.Equal("الرسوم", text.Get("ar", "en"));
        Assert.Equal("Fee", text.Get("ur", "en"));
        Assert.Equal("Fee", text.Get("en-GB"));
        Assert.Equal("الرسوم", LocalizedText.Of("ar", "الرسوم").Get("fr", "en"));
    }

    [Fact]
    public void Plain_text_stored_before_languages_were_dynamic_reads_as_english()
    {
        Assert.Equal("Trade Name Reservation", LocalizedText.Parse("Trade Name Reservation")["en"]);
        Assert.Equal("حجز", LocalizedText.Parse("""{"en":"Reserve","ar":"حجز"}""")["ar"]);
        Assert.Equal("x", JsonSerializer.Deserialize<LocalizedText>("\"x\"")!["en"]);
    }

    [Fact]
    public void Message_references_render_in_each_readers_language()
    {
        var message = MessageRef.Of("status.notify.rejected", ("reason", "Name taken"));
        var json = JsonSerializer.Serialize(message);
        var back = JsonSerializer.Deserialize<MessageRef>(json)!;

        Assert.Equal("Your application was not approved. Reason: Name taken", back.Render(TestText.Messages("en")));
        Assert.StartsWith("لم تتم الموافقة", back.Render(TestText.Messages("ar")));
        Assert.Equal("Old text", JsonSerializer.Deserialize<MessageRef>("\"Old text\"")!.Render(TestText.Messages()));
    }
}

public class ServiceDefinitionJsonTests
{
    [Fact]
    public void Definitions_saved_with_separate_arabic_fields_are_upgraded()
    {
        var old = """
            {"code":"X","name":"Permit","nameAr":"تصريح","description":"D","steps":[{"id":"a","title":"A","titleAr":"أ",
             "fields":[{"key":"k","label":"Label","labelAr":"تسمية","options":[{"value":"1","label":"One","labelAr":"واحد"}]}]}]}
            """;
        var d = ServiceDefinitionJson.Deserialize(old);

        Assert.Equal("تصريح", d.Name["ar"]);
        Assert.Equal("Permit", d.Name["en"]);
        Assert.Equal("أ", d.Steps[0].Title["ar"]);
        Assert.Equal("واحد", d.Steps[0].Fields[0].Options![0].Label["ar"]);
        Assert.Equal("تصريح", ServiceDefinitionJson.Deserialize(ServiceDefinitionJson.Serialize(d)).Name["ar"]);
    }
}

public class AiPromptTests
{
    [Fact]
    public void Placeholders_are_filled_and_other_braces_are_left_alone()
    {
        var text = Dsp.Contracts.AiPrompts.Fill("{portal} of {ministry}. Keep {other}.", "Ministry X", "Portal Y");

        Assert.Equal("Portal Y of Ministry X. Keep {other}.", text);
    }

    [Fact]
    public void Both_default_prompts_use_the_configurable_ministry_name()
    {
        Assert.Contains("{ministry}", Dsp.Contracts.AiPrompts.DefaultChat);
        Assert.Contains("{ministry}", Dsp.Contracts.AiPrompts.DefaultReview);
        Assert.DoesNotContain("Economy", Dsp.Contracts.AiPrompts.DefaultChat + Dsp.Contracts.AiPrompts.DefaultReview);
    }

    [Fact]
    public void Settings_keep_a_custom_prompt_and_refuse_one_that_is_too_long()
    {
        var settings = Dsp.Platform.Domain.AiSettings.CreateDefault();
        settings.Update("", "", "", 0.2, 90, false, 6, 30, null, null, "  Be kind.  ", null, "staff");

        Assert.Equal("Be kind.", settings.ChatPrompt);
        Assert.Null(settings.ReviewPrompt);
        Assert.Throws<Dsp.SharedKernel.ValidationException>(() =>
            settings.Update("", "", "", 0.2, 90, false, 6, 30, null, null, new string('a', 8001), null, "staff"));
    }
}

public class LanguageVisibilityTests
{
    [Fact]
    public void A_new_language_is_hidden_from_citizens_until_staff_show_it()
    {
        var language = Language.Create("ur", new LocalizedText { ["en"] = "Urdu" }, "rtl", 30, "staff");

        Assert.False(language.ShowInPortal);
        Assert.True(language.ShowInBackoffice);

        language.SetVisibility(true, false, "staff");
        Assert.True(language.ShowInPortal);
        Assert.False(language.ShowInBackoffice);
    }

    [Fact]
    public void The_default_language_is_always_shown_in_both_portals()
    {
        var en = Language.Create("en", new LocalizedText { ["en"] = "English" }, "ltr", 0, "staff");
        LanguagePolicy.SetDefault([en], en, "staff");

        Assert.True(en.ShowInPortal);
        Assert.True(en.ShowInBackoffice);
        Assert.Throws<DomainException>(() => en.SetVisibility(false, true, "staff"));
    }
}
