using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SquidDraftLeague.Language.Resources;

namespace SquidDraftLeague.Draft.Matchmaking
{
    public static class Matchmaker
    {
        public static readonly ReadOnlyCollection<Lobby> Lobbies = new List<Lobby>
        {
            new Lobby(1),
            new Lobby(2),
            new Lobby(3),
            new Lobby(4),
            new Lobby(5),
            new Lobby(6),
            new Lobby(7),
            new Lobby(8),
            new Lobby(9),
            new Lobby(10)
        }.AsReadOnly();

        public static readonly ReadOnlyCollection<Set> Sets = new List<Set>
        {
            new Set(1),
            new Set(2),
            new Set(3),
            new Set(4),
            new Set(5)
        }.AsReadOnly();

        public static LobbyEligibilityResponse LobbyEligibility(ulong discordId)
        {
            try
            {
                if (Sets.Any(e => e.AllPlayers.Any(f => f.DiscordId == discordId)))
                {
                    return new LobbyEligibilityResponse(false, Resources.JoinLobbyInSet);
                }

                if (Lobbies.Any(e => e.Players.Any(f => f.DiscordId == discordId)))
                {
                    return new LobbyEligibilityResponse(false, Resources.JoinLobbyInLobby);
                }

                return new LobbyEligibilityResponse(true);
            }
            catch (Exception e)
            {
                return new LobbyEligibilityResponse(false, exception: e);
            }
        }

        public static LobbySelectResponse SelectLobbyByNumber(SdlPlayer sdlPlayer, int lobbyNumber)
        {
            try
            {
                Lobby selectedLobby = Lobbies[lobbyNumber - 1];

                if (selectedLobby.IsWithinThreshold(sdlPlayer.PowerLevel))
                {
                    return new LobbySelectResponse(true, result: selectedLobby);
                }

                if ((DateTime.Now - selectedLobby.StartTime).TotalMinutes >= 5 && selectedLobby.IsWithinThreshold(sdlPlayer.PowerLevel - 100))
                {
                    if ((selectedLobby.Halved?.PowerLevel ?? 0) < sdlPlayer.PowerLevel)
                        selectedLobby.Halved = sdlPlayer;

                    return new LobbySelectResponse(true,
                        "You will be added to this lobby but please note that winning will cause you to gain half the points.",
                        result: selectedLobby);
                }

                return new LobbySelectResponse(false, $"You are not eligible to join lobby #{lobbyNumber}.");
            }
            catch (Exception e)
            {
                return new LobbySelectResponse(false, exception: e);
            }
        }

        public static LobbySelectResponse FindLobby(SdlPlayer sdlPlayer)
        {
            try
            {
                List<Lobby> matchedLobbies =
                    Lobbies.Where(e => !e.IsFull && e.IsWithinThreshold(sdlPlayer.PowerLevel)).ToList();

                if (matchedLobbies.Any())
                {
                    return new LobbySelectResponse(true, result:
                        matchedLobbies.OrderBy(e => Math.Abs(e.LobbyPowerLevel - sdlPlayer.PowerLevel)).First());
                }

                if (Lobbies.All(e => e.Players.Any()))
                {
                    return new LobbySelectResponse(false, Resources.LobbiesFull);
                }

                return new LobbySelectResponse(true, result: 
                    Lobbies.First(e => !e.Players.Any()));
            }
            catch (Exception e)
            {
                return new LobbySelectResponse(false, exception: e);
            }
        }

        public static MoveToSetResponse MoveLobbyToSet(Lobby matchedLobby)
        {
            try
            {
                if (Sets.All(e => e.AllPlayers.Any()))
                {
                    matchedLobby.InStandby = true;
                    return new MoveToSetResponse(false, string.Format(Resources.SetsFull, "<#579890960394354690>"));
                }

                Set newSet = Sets.First(e => !e.AllPlayers.Any());
                newSet.MoveLobbyToSet(matchedLobby);
                matchedLobby.Close();

                return new MoveToSetResponse(true, result: newSet);
            }
            catch (Exception e)
            {
                return new MoveToSetResponse(false, exception: e);
            }
        }

        public static PickPlayerResponse PickSetPlayer(SdlPlayer pick, Set playerMatch)
        {
            try
            {
                if (playerMatch.DraftPlayers.All(e => e.DiscordId != pick.DiscordId))
                {
                    return new PickPlayerResponse(false, "This player is not available to be drafted.");
                }

                SdlPlayer sdlPick = playerMatch.DraftPlayers.Find(e => e.DiscordId == pick.DiscordId);
                playerMatch.GetPickingTeam().AddPlayer(sdlPick);

                playerMatch.DraftPlayers.Remove(sdlPick);

                PickPlayerResponse pickPlayerResponse = new PickPlayerResponse(true);

                if (playerMatch.DraftPlayers.Count == 1)
                {
                    playerMatch.GetPickingTeam().AddPlayer(playerMatch.DraftPlayers.First());

                    playerMatch.DraftPlayers.Clear();

                    pickPlayerResponse.LastPlayer = true;
                }

                playerMatch.ResetTimeout();

                return pickPlayerResponse;
            }
            catch (Exception e)
            {
                return new PickPlayerResponse(false, exception: e);
            }
        }
    }
}