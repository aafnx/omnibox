using OmniHub.Platform;

namespace OmniHub.Status.Collectors;

internal static class Ports
{
    /// <summary>Строка о порте: кто слушает и на каких адресах. Доступ из сети — повод посмотреть.</summary>
    public static StatusItem Describe(string id, string label, List<PortListener> all, int port, bool warnIfClosed)
    {
        List<PortListener> mine = all.Where(l => l.Port == port).ToList();
        if (mine.Count == 0)
        {
            return new StatusItem(id, label, StatusTexts.PortClosed, warnIfClosed ? Level.Warn : Level.Info)
                .With("port", port)
                .With("listening", false);
        }

        bool exposed = mine.Any(l => !l.IsLoopback);
        string owners = string.Join(", ", mine.Select(l => l.Pid).Distinct().Select(pid => Win.ProcessName(pid) ?? pid.ToString()));
        string addresses = string.Join(", ", mine.Select(l => l.Address.ToString()).Distinct());
        string value = $"{addresses} ({owners}) — {(exposed ? StatusTexts.PortAllInterfaces : StatusTexts.PortLoopback)}";
        return new StatusItem(id, label, value, exposed ? Level.Warn : Level.Ok)
            .With("port", port)
            .With("listening", true)
            .With("exposed", exposed)
            .With("addresses", mine.Select(l => l.Address.ToString()).Distinct().ToArray())
            .With("processes", mine.Select(l => Win.ProcessName(l.Pid) ?? l.Pid.ToString()).Distinct().ToArray());
    }
}
