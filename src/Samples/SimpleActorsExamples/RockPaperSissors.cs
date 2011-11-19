using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleActorsExamples
{
    using SimpleConcurrency.Actors;
    using System.Security.Cryptography;

    struct RPSMessage
    {
        public enum RPS
        {
            Rock,
            Paper,
            Scissors,
            Go,
            End
        }

        public RPS Order { get; set; }
        public string Player { get; set; }
    }

    class RPSCoordinator : SimpleActor<RPSMessage>
    {
        static int[,] __result = {
                                     { 0, -1, 1 },
                                     { 1, 0, -1 },
                                     { -1, 1, 0 }
                                 };

        protected override void Act()
        {
            while (true)
            {
                RPSMessage message_1 = new RPSMessage();
                SimpleActor<RPSMessage> player_1 = null;
                Receive((m1, s1) =>
                {
                    message_1 = m1;
                    player_1 = s1;
                });

                if (message_1.Order == RPSMessage.RPS.End) break;

                RPSMessage message_2 = new RPSMessage();
                SimpleActor<RPSMessage> player_2 = null;
                Receive((m2, s2) =>
                {
                    message_2 = m2;
                    player_2 = s2;
                });

                if (message_2.Order == RPSMessage.RPS.End) break;

                Console.WriteLine("{0} => {1}", message_1.Player, message_1.Order);
                Console.WriteLine("{0} => {1}", message_2.Player, message_2.Order);
                switch (__result[(int)message_1.Order, (int)message_2.Order])
                {
                    default:
                    case 0:
                        Console.WriteLine("Draw!");
                        break;

                    case -1:
                        Console.WriteLine(message_2.Player + " won!");
                        break;

                    case 1:
                        Console.WriteLine(message_1.Player + " won!");
                        break;
                }
                Console.WriteLine();

                System.Threading.Thread.Sleep(1000);
                player_1.Post(new RPSMessage() { Order = RPSMessage.RPS.Go }, this);
                player_2.Post(new RPSMessage() { Order = RPSMessage.RPS.Go }, this);
            }
            Console.WriteLine("Referee: I quit");
        }
    }

    class RPSPlayer : SimpleActor<RPSMessage>
    {
        string _name;
        Random _rng;

        public RPSPlayer(string name)
        {
            _name = name;
            var p = new RNGCryptoServiceProvider();
            byte[] b = new byte[1];
            p.GetBytes(b);
            _rng = new Random((int)b[0]);
        }

        protected override void Act()
        {
            LoopReact((m, s) =>
            {
                switch (m.Order)
                {
                    case RPSMessage.RPS.End:
                        Console.WriteLine(_name + ": I quit");
                        return false;

                    case RPSMessage.RPS.Go:
                        var message = new RPSMessage();
                        message.Player = _name;
                        message.Order = (RPSMessage.RPS)_rng.Next(3);
                        s.Post(message, this);
                        break;

                    default:
                        break;
                }
                return true;
            });
        }
    }
}
