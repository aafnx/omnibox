using System.Text;
using System.Text.Json;
using OmniHub.Catalog;
using OmniHub.Platform;
using OmniHub.Status;
using OmniHub.Status.Collectors;

namespace OmniHub.Tests;

public class StatusTests
{
    [Fact]
    public void Catalog_is_embedded_and_valid()
    {
        OmniCatalog catalog = OmniCatalog.Load();
        Assert.Equal("SteamDeckHomebrew/decky-loader", catalog.DeckyRepository);
        Assert.NotEmpty(catalog.DeckyBuilds);
        Assert.All(catalog.DeckyBuilds, b => Assert.Matches("^[0-9a-f]{64}$", b.Sha256));
        Assert.All(catalog.Movies, m => Assert.Matches("^[0-9a-f]{64}$", m.Sha256));
        Assert.All(catalog.Movies, m => Assert.StartsWith("omnibox-", m.File, StringComparison.Ordinal));
        Assert.NotNull(catalog.FindDecky(catalog.DeckyBuilds[0].Sha256.ToUpperInvariant()));
    }

    [Fact]
    public void Steam_manifest_version_and_cfg()
    {
        string manifest = "\"ui\"\n{\n\t\"version\"\t\t\"1760123456\"\n\t\"other\"\t\"1\"\n}\n";
        Assert.Equal(1760123456L, Steam.ParseClientVersion(manifest));
        Assert.Null(Steam.ParseClientVersion("nothing here"));
        Assert.NotNull(Steam.VersionDate(1760123456));
        Assert.Null(Steam.VersionDate(42));
        Assert.True(Steam.UpdatesBlocked("BootStrapperInhibitAll=enable\r\n"));
        Assert.True(Steam.UpdatesBlocked("x=1\nBootStrapperInhibitAll = Enable\n"));
        Assert.False(Steam.UpdatesBlocked("BootStrapperInhibitAll=disable"));
    }

    [Fact]
    public void Vrr_setting_is_parsed()
    {
        Assert.True(Vrr.Parse("SwapEffectUpgradeEnable=1;VRROptimizeEnable=1;"));
        Assert.False(Vrr.Parse("VRROptimizeEnable=0;"));
        Assert.Null(Vrr.Parse("SwapEffectUpgradeEnable=1;"));
        Assert.Null(Vrr.Parse(null));
    }

    [Fact]
    public void Tcp_port_is_read_in_network_order()
    {
        Assert.Equal(1337, TcpListeners.PortFromNetworkOrder(0x3905));
        Assert.Equal(8080, TcpListeners.PortFromNetworkOrder(0x901F));
    }

    [Fact]
    public void Edid_manufacturer_is_decoded()
    {
        Assert.Equal("GSM", Displays.DecodeEdidManufacturer(0x6D1E)); // LG
        Assert.Equal("HDMI", Displays.ConnectorName(5));
    }

    [Fact]
    public void Native_structs_have_windows_sizes()
    {
        // Размеры из заголовков Windows SDK для x64: если раскладка структуры съедет, чтение экранов и Bluetooth сломается.
        Assert.Equal(72, Displays.SizeOfPathInfo);
        Assert.Equal(64, Displays.SizeOfModeInfo);
        Assert.Equal(420, Displays.SizeOfTargetDeviceName);
        Assert.Equal(84, Displays.SizeOfSourceDeviceName);
        Assert.Equal(40, BluetoothDevices.SizeOfSearchParams);
        Assert.Equal(560, BluetoothDevices.SizeOfDeviceInfo);
        Assert.Equal(520, BluetoothDevices.SizeOfRadioInfo);
    }

    [Fact]
    public void Bluetooth_address_and_class()
    {
        var device = new BluetoothDevice("Наушники", 0x001122AABBCC, 0x240404, true, true, true);
        Assert.Equal("00:11:22:AA:BB:CC", device.AddressText);
        Assert.Equal(4, device.MajorClass); // аудио
    }

    [Fact]
    public void Task_xml_with_russian_text_is_parsed()
    {
        string xml = """
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>Запуск Decky для OMNIBOX</Description></RegistrationInfo>
              <Triggers><LogonTrigger><Enabled>true</Enabled></LogonTrigger></Triggers>
              <Principals><Principal id="Author"><UserId>OMNIBOX\Пользователь</UserId><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
              <Settings><Enabled>true</Enabled><ExecutionTimeLimit>PT0S</ExecutionTimeLimit></Settings>
              <Actions Context="Author">
                <Exec><Command>C:\Users\Тест Пользователь\homebrew\services\PluginLoader_noconsole.exe</Command></Exec>
              </Actions>
            </Task>
            """;
        ScheduledTask task = ScheduledTasks.ParseXml(xml, @"\OMNIBOX\Decky");
        Assert.True(task.Enabled);
        Assert.Equal(new[] { "LogonTrigger" }, task.Triggers);
        Assert.Single(task.Commands);
        Assert.Contains("Тест Пользователь", task.Commands[0], StringComparison.Ordinal);
        Assert.Equal(@"OMNIBOX\Пользователь", task.UserId);
        Assert.False(task.Highest);
        Assert.True(AutostartCollector.IsRelevant(task));
    }

    [Fact]
    public void Plugin_with_russian_name_is_read()
    {
        string dir = Path.Combine(Path.GetTempPath(), "omnihub-plugin-" + Guid.NewGuid().ToString("N"), "Плагин «тест»");
        Directory.CreateDirectory(dir);
        try
        {
            // plugin.json с BOM, как его иногда сохраняет Блокнот.
            File.WriteAllText(Path.Combine(dir, "plugin.json"), "{\"name\":\"Погода\",\"author\":\"Автор\"}", new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(dir, "package.json"), "{\"version\":\"1.2.3\"}");
            File.WriteAllText(Path.Combine(dir, ".playhub-release.json"), "{}");
            PluginInfo? p = PluginReader.Read(dir);
            Assert.NotNull(p);
            Assert.Equal("Погода", p.Name);
            Assert.Equal("Автор", p.Author);
            Assert.Equal("1.2.3", p.Version);
            Assert.Equal("Плагин «тест»", p.Folder);
            Assert.True(p.ByPlayhub);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [Fact]
    public void Status_collects_every_section_and_writes_ascii_json()
    {
        StatusReport report = StatusRunner.Collect();
        Assert.Equal(StatusRunner.SectionIds, report.Sections.Select(s => s.Id).ToArray());

        string json = StatusWriter.ToJson(report);
        Assert.True(Ascii.IsValid(json));
        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Equal("status", doc.RootElement.GetProperty("command").GetString());
        Assert.Equal(StatusRunner.SectionIds.Length, doc.RootElement.GetProperty("sections").GetArrayLength());

        string text = StatusWriter.ToText(report);
        Assert.Contains(StatusTexts.SystemTitle, text, StringComparison.Ordinal);
        Assert.Contains(StatusTexts.Legend, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Worst_level_bubbles_up()
    {
        var section = new StatusSection("x", "X");
        StatusItem item = section.Add("a", "A", null, Level.Ok);
        item.Add(new StatusItem("b", "B", null, Level.Warn));
        Assert.Equal(Level.Warn, section.WorstLevel());
    }

    [Fact]
    public void Windows_name_from_registry()
    {
        Assert.Equal("Windows 11", SystemCollector.WindowsName("Windows 10 Pro", "Client", 26100));
        Assert.Equal("Windows 10", SystemCollector.WindowsName("Windows 10 Pro", "Client", 19045));
        Assert.Equal("Windows Server 2025 Datacenter", SystemCollector.WindowsName("Windows Server 2025 Datacenter", "Server", 26100));
    }
}
