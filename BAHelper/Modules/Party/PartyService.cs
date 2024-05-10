﻿using System.Collections.Generic;
using ECommons.Automation.LegacyTaskManager;
namespace BAHelper.Modules.Party;

public class PartyService
{
    private readonly TaskManager TaskManager = new();
    // note: "Portal" means the unstable / stable portal, not the light-green one in the BA dungeon
    private static readonly List<List<(float X, float Y)>> Portals =
    [
        [(4.2f, 15.3f), (7.2f, 14.7f), (10.5f, 14.6f), (6.9f, 18.5f), (10.1f, 18.0f), (6.2f, 21.7f), (4.3f, 26.2f), (7.3f, 29.3f)],
        [(15.8f, 14.5f), (16.4f, 18.7f), (10.6f, 21.9f), (15.9f, 22.3f), (13.5f, 24.2f), (10.7f, 25.9f), (14.7f, 26.8f), (16.7f, 29.2f)],
        [(21.2f, 17.1f), (19.3f, 19.9f), (23.7f, 21.8f), (19.1f, 26.8f), (23.0f, 26.9f), (19.1f, 28.6f), (20.7f, 28.9f), (22.8f, 29.1f)],
        [(25.0f, 16.3f), (26.6f, 18.9f), (28.8f, 23.3f), (25.1f, 25.4f), (26.7f, 26.7f), (26.0f, 27.7f), (25.4f, 28.9f), (25.6f, 30.2f)],
        [(28.8f, 15.3f), (33.3f, 17.0f), (29.9f, 19.5f), (32.4f, 24.7f), (29.4f, 26.3f), (30.7f, 28.5f), (30.2f, 30.1f), (31.7f, 29.9f)],
        [(35.3f, 13.8f), (37.9f, 15.0f), (36.0f, 19.0f), (37.4f, 23.8f), (33.0f, 27.7f), (37.3f, 27.8f), (32.6f, 28.8f), (35.8f, 29.9f)]
    ];

    public void SendPortalsToChat(int partyNumber)
    {
        if (partyNumber < 1 || partyNumber > 6)
            return;
        TaskManager.Enqueue(() => Game.SendMessage($"/p 我们是{partyNumber}队，门图如下："));
        for (int i = 0; i < 8; i++)
        {
            TaskManager.DelayNext(100);
            var (X, Y) = Portals[partyNumber - 1][i];
            TaskManager.Enqueue(() => Utils.SetFlagMarker(827, 515, X, Y));
            var portalNumber = i + 1;
            TaskManager.Enqueue(() => Game.SendMessage($"/p {partyNumber}{portalNumber} - <flag>"));
        }
    }
}
