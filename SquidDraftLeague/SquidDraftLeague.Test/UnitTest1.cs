using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

namespace Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [TestCase(26)]
        [TestCase(64)]
        [TestCase(80)]
        [TestCase(91)]
        [TestCase(79)]
        [TestCase(69)]
        public void CheckIfRoundsAreFair(int playerCount)
        {
            int roundCount;
            for (roundCount = 4; roundCount > 1; roundCount--)
            {
                if (playerCount / (roundCount * 4) >= 4)
                {
                    break;
                }
            }

            int minimumRequired = playerCount / (4 * roundCount);

            if (roundCount == 1)
            {
                if (minimumRequired - 4 == 3)
                {
                    roundCount = 2;
                    minimumRequired = 3;
                }
            }

            int remainingPlayers = playerCount % (4 * roundCount);
            int distributeNum = (minimumRequired * 4 + remainingPlayers) / 4;

            List<int> roundCaptainAmounts = new List<int> { distributeNum };

            for (int i = 1; i < roundCount; i++)
            {
                roundCaptainAmounts.Add(minimumRequired);
            }

            if (roundCaptainAmounts.Count > 1)
            {
                while (roundCaptainAmounts[0] - 1 > roundCaptainAmounts[1])
                {
                    for (int i = 1; i < 4; i++)
                    {
                        roundCaptainAmounts[0] -= 1;
                        roundCaptainAmounts[i] += 1;

                        if (roundCaptainAmounts[0] - 1 < roundCaptainAmounts[1])
                        {
                            break;
                        }
                    }
                }
            }

            Debug.WriteLine($"Round Counts for {playerCount}: {string.Join(" ", roundCaptainAmounts)}");
        }
    }
}