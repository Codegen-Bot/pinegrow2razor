using System.Threading;
using System.Threading.Tasks;

namespace Pinegrow2Razor;

/// <summary>
/// New bots include the concept of a mini bot. You can put all your bot code in one or more mini bots.
/// Mini bots can be moved from one bot's code to another, making it easy to refactor bots.
/// </summary>
public interface IMiniBot
{
    void Execute();
}
