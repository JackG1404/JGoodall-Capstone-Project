using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CinemaPOS
{
    public enum StaffLevel { General, Manager }

    public class Staff
    {
        public string ID { get; set; }
        public StaffLevel Level { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public override string ToString() => $"{ID} - {FirstName} {LastName} ({Level})";
    }

    public static class StaffLoader
    {
        public static List<Staff> LoadStaff(string[] lines)
        {
            var staffList = new List<Staff>();
            foreach (var line in lines)
            {
                if (!line.StartsWith("[Staff:")) continue;
                var parts = line.Trim('[', ']').Split('%');
                var id = parts[0].Split(':')[1];
                var level = parts[1].Split(':')[1] == "Manager" ? StaffLevel.Manager : StaffLevel.General;
                var firstName = parts[2].Split(':')[1];
                var lastName = parts[3].Split(':')[1];

                staffList.Add(new Staff { ID = id, Level = level, FirstName = firstName, LastName = lastName });
            }
            return staffList;
        }
    }

    public class Member
    {
        public string MembershipID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public int VisitCount { get; set; }
        public DateTime? GoldExpiry { get; set; }
    }

    public static class MemberData
    {
        private const string MemberFile = "members.txt";

        public static List<Member> LoadMembers()
        {
            var members = new List<Member>();
            if (!File.Exists(MemberFile))
            {
                File.WriteAllText(MemberFile, "");
                return members;
            }

            foreach (var line in File.ReadAllLines(MemberFile))
            {
                var parts = line.Split('%');
                members.Add(new Member
                {
                    MembershipID = parts[0],
                    FirstName = parts[1],
                    LastName = parts[2],
                    Email = parts[3],
                    VisitCount = int.Parse(parts[4]),
                    GoldExpiry = string.IsNullOrWhiteSpace(parts[5]) ? null : DateTime.Parse(parts[5])
                });
            }
            return members;
        }

        public static void SaveMembers(List<Member> members)
        {
            var lines = members.Select(m =>
                $"{m.MembershipID}%{m.FirstName}%{m.LastName}%{m.Email}%{m.VisitCount}%{(m.GoldExpiry.HasValue ? m.GoldExpiry.Value.ToString("yyyy-MM-dd") : "")}");
            File.WriteAllLines(MemberFile, lines);
        }
    }

    public class Transaction
    {
        public string StaffID { get; set; }
        public string? MemberID { get; set; }
        public List<string> Items { get; set; } = new();
        public int TotalPrice { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public static class TransactionData
    {
        private const string TransactionFile = "transactions.txt";

        public static void SaveTransaction(Transaction tx)
        {
            string line = $"{tx.Timestamp:yyyy-MM-dd HH:mm}%{tx.StaffID}%{tx.MemberID ?? ""}%{string.Join(",", tx.Items)}%{tx.TotalPrice}";
            File.AppendAllLines(TransactionFile, new[] { line });
        }

        public static List<Transaction> LoadTransactions()
        {
            var list = new List<Transaction>();
            if (!File.Exists(TransactionFile))
            {
                File.WriteAllText(TransactionFile, "");
                return list;
            }

            foreach (var line in File.ReadAllLines(TransactionFile))
            {
                var parts = line.Split('%');
                list.Add(new Transaction
                {
                    Timestamp = DateTime.Parse(parts[0]),
                    StaffID = parts[1],
                    MemberID = string.IsNullOrWhiteSpace(parts[2]) ? null : parts[2],
                    Items = parts[3].Split(',').ToList(),
                    TotalPrice = int.Parse(parts[4])
                });
            }
            return list;
        }
    }

    public static class ScheduleManager
    {
        public static void ManageSchedule()
        {
            Console.WriteLine("Enter date (yyyy-MM-dd):");
            var dateInput = Console.ReadLine();
            if (!DateTime.TryParse(dateInput, out var date))
            {
                Console.WriteLine("Invalid date.");
                return;
            }
            var schedule = Schedule.Load(date);

            while (true)
            {
                Console.WriteLine("\nSchedule Menu:");
                Console.WriteLine("1. View Schedule");
                Console.WriteLine("2. Add Screening");
                Console.WriteLine("3. Save Schedule");
                Console.WriteLine("0. Back");
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        schedule.Display();
                        break;
                    case "2":
                        Console.Write("Film Title: ");
                        var title = Console.ReadLine();
                        Console.Write("Start Time (HH:mm): ");
                        var timeInput = Console.ReadLine();
                        Console.Write("Screen: ");
                        var screen = Console.ReadLine();
                        Console.Write("Duration (min): ");
                        var durationStr = Console.ReadLine();
                        Console.Write("Seats: ");
                        var seatsStr = Console.ReadLine();

                        if (!TimeSpan.TryParse(timeInput, out var startTime) ||
                            !int.TryParse(durationStr, out int duration) ||
                            !int.TryParse(seatsStr, out int seats))
                        {
                            Console.WriteLine("Invalid input.");
                            continue;
                        }

                        var screening = new Screening
                        {
                            FilmTitle = title,
                            StartTime = startTime,
                            Duration = duration,
                            Screen = screen,
                            Seats = seats
                        };

                        if (!schedule.AddScreening(screening))
                            Console.WriteLine("Conflict detected or invalid schedule.");
                        else
                            Console.WriteLine("Screening added.");
                        break;
                    case "3":
                        schedule.Save(date);
                        Console.WriteLine("Schedule saved.");
                        break;
                    case "0": return;
                    default: Console.WriteLine("Invalid choice."); break;
                }
            }
        }
    }

    public class Screening
    {
        public string FilmTitle { get; set; }
        public TimeSpan StartTime { get; set; }
        public int Duration { get; set; }
        public string Screen { get; set; }
        public int Seats { get; set; }
        public TimeSpan EndTime => StartTime.Add(TimeSpan.FromMinutes(Duration));
        public TimeSpan Turnaround => Seats <= 50 ? TimeSpan.FromMinutes(15) : Seats <= 100 ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(45);
    }

    public class Schedule
    {
        private List<Screening> Screenings { get; set; } = new();

        public bool AddScreening(Screening screening)
        {
            var conflict = Screenings.Any(s => s.Screen == screening.Screen &&
                screening.StartTime < s.EndTime.Add(s.Turnaround) &&
                screening.EndTime.Add(screening.Turnaround) > s.StartTime);
            if (conflict) return false;
            Screenings.Add(screening);
            return true;
        }

        public void Display()
        {
            if (!Screenings.Any())
            {
                Console.WriteLine("No screenings scheduled.");
                return;
            }

            foreach (var s in Screenings.OrderBy(s => s.StartTime))
            {
                Console.WriteLine($"{s.StartTime:hh\\:mm} - {s.EndTime:hh\\:mm} | {s.FilmTitle} | Screen {s.Screen} | Seats: {s.Seats}");
            }
        }

        public void Save(DateTime date)
        {
            var path = $"{date:yyyy-MM-dd}.fs";
            var lines = Screenings.Select(s => $"{s.FilmTitle}%{s.StartTime}%{s.Duration}%{s.Screen}%{s.Seats}");
            File.WriteAllLines(path, lines);
        }

        public static Schedule Load(DateTime date)
        {
            var schedule = new Schedule();
            var path = $"{date:yyyy-MM-dd}.fs";
            if (!File.Exists(path)) return schedule;
            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split('%');
                schedule.Screenings.Add(new Screening
                {
                    FilmTitle = parts[0],
                    StartTime = TimeSpan.Parse(parts[1]),
                    Duration = int.Parse(parts[2]),
                    Screen = parts[3],
                    Seats = int.Parse(parts[4])
                });
            }
            return schedule;
        }
    }

    class Program
    {
        static List<Staff> staffList = new();
        static Staff? loggedInStaff = null;
        static List<Member> memberList = new();

        static void Main(string[] args)
        {
            if (!File.Exists("cinema.txt"))
                File.WriteAllText("cinema.txt", "");

            string[] cinemaFile = File.ReadAllLines("cinema.txt");
            staffList = StaffLoader.LoadStaff(cinemaFile);
            memberList = MemberData.LoadMembers();

            Console.WriteLine("Welcome to Hullywood Cinema POS System");
            Console.WriteLine("Please log in:");

            for (int i = 0; i < staffList.Count; i++)
                Console.WriteLine($"{i + 1}. {staffList[i]}");

            int selectedIndex;
            while (true)
            {
                Console.Write("Enter number to log in: ");
                if (int.TryParse(Console.ReadLine(), out selectedIndex) && selectedIndex >= 1 && selectedIndex <= staffList.Count)
                {
                    loggedInStaff = staffList[selectedIndex - 1];
                    break;
                }
                Console.WriteLine("Invalid selection, try again.");
            }

            Console.WriteLine($"Logged in as {loggedInStaff.FirstName} {loggedInStaff.LastName} ({loggedInStaff.Level})");
            ShowMenu();
        }

        static void ShowMenu()
        {
            while (true)
            {
                Console.WriteLine("\nMain Menu:");
                Console.WriteLine("1. Start New Transaction");
                Console.WriteLine("2. Load Member Data");
                if (loggedInStaff!.Level == StaffLevel.Manager)
                {
                    Console.WriteLine("3. Manage Schedule");
                    Console.WriteLine("4. Manage Staff (not implemented)");
                    Console.WriteLine("5. View All Transactions");
                }
                Console.WriteLine("0. Exit");

                Console.Write("Select an option: ");
                string? input = Console.ReadLine();
                switch (input)
                {
                    case "1":
                        StartTransaction();
                        break;
                    case "2":
                        memberList = MemberData.LoadMembers();
                        Console.WriteLine($"{memberList.Count} members loaded.");
                        break;
                    case "3":
                        if (loggedInStaff.Level == StaffLevel.Manager)
                            ScheduleManager.ManageSchedule();
                        break;
                    case "4":
                        if (loggedInStaff.Level == StaffLevel.Manager)
                            Console.WriteLine("Managing staff... (not implemented yet)");
                        break;
                    case "5":
                        if (loggedInStaff.Level == StaffLevel.Manager)
                            ViewAllTransactions();
                        break;
                    case "0":
                        Console.WriteLine("Exiting...");
                        return;
                    default:
                        Console.WriteLine("Invalid choice.");
                        break;
                }
            }
        }

        static void StartTransaction()
        {
            var tx = new Transaction { StaffID = loggedInStaff!.ID };

            Console.Write("Enter Member ID (or press Enter for guest): ");
            string? mid = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(mid))
            {
                var member = memberList.FirstOrDefault(m => m.MembershipID == mid);
                if (member != null)
                {
                    tx.MemberID = mid;
                    member.VisitCount++;
                    Console.WriteLine($"Welcome {member.FirstName}! Visits: {member.VisitCount}");
                }
                else Console.WriteLine("Member not found.");
            }

            string screeningFile = "screenings.txt";
            var screenings = File.Exists(screeningFile) ? File.ReadAllLines(screeningFile) : Array.Empty<string>();

            while (true)
            {
                Console.WriteLine("\nAdd Ticket to Transaction:");
                Console.WriteLine("Are you an Adult (Adult tickets are £10.00) or a Child? (Child tickets are £7.00) (A/C, or Enter to skip): ");
                string typeInput = Console.ReadLine()?.ToUpper();
                if (string.IsNullOrWhiteSpace(typeInput)) break;
                if (typeInput != "A" && typeInput != "C")
                {
                    Console.WriteLine("Invalid input.");
                    continue;
                }

                if (screenings.Length == 0)
                {
                    Console.WriteLine("No screenings available.");
                    break;
                }

                Console.WriteLine("Available Screenings:");
                for (int i = 0; i < screenings.Length; i++)
                {
                    var parsed = screenings[i].Trim('[', ']').Split('%')
                        .Select(p => p.Split(':'))
                        .ToDictionary(p => p[0], p => p[1]);

                    Console.WriteLine($"{i + 1}. {parsed["Movie"]} ({parsed["Genre"]}, {parsed["Length"]} min, Rated {parsed["Rating"]})");
                }

                Console.Write("Select a screening (0 to cancel): ");
                if (!int.TryParse(Console.ReadLine(), out int selected) || selected < 1 || selected > screenings.Length)
                {
                    Console.WriteLine("Cancelled or invalid selection.");
                    continue;
                }

                string selectedScreening = screenings[selected - 1];
                string ticketType = typeInput == "A" ? "Adult" : "Child";
                int price = typeInput == "A" ? 1000 : 700;
                tx.Items.Add($"{ticketType} Ticket: {selectedScreening}");
                tx.TotalPrice += price;
                Console.WriteLine("Ticket added to transaction.");
            }

            var priceList = new Dictionary<string, int>
        {
            { "Popcorn", 500 },
            { "Drink", 300 }
        };

            while (true)
            {
                Console.WriteLine("\nAdd item to transaction:");
                int index = 1;
                foreach (var item in priceList)
                    Console.WriteLine($"{index++}. {item.Key} - £{item.Value / 100.0:F2}");
                Console.WriteLine("0. Finish transaction");

                Console.Write("Choice: ");
                if (!int.TryParse(Console.ReadLine(), out int choice) || choice < 0 || choice > priceList.Count)
                {
                    Console.WriteLine("Invalid choice.");
                    continue;
                }
                if (choice == 0) break;

                var selected = priceList.ElementAt(choice - 1);
                tx.Items.Add(selected.Key);
                tx.TotalPrice += selected.Value;
                Console.WriteLine($"Added {selected.Key}. Total: £{tx.TotalPrice / 100.0:F2}");
            }

            TransactionData.SaveTransaction(tx);
            MemberData.SaveMembers(memberList);
            Console.WriteLine($"Transaction saved. Final total: £{tx.TotalPrice / 100.0:F2}");
        }

        static void ViewAllTransactions()
        {
            var allTx = TransactionData.LoadTransactions();
            Console.WriteLine($"\n--- All Transactions ({allTx.Count}) ---");
            foreach (var tx in allTx)
            {
                string items = string.Join(", ", tx.Items);
                string memberInfo = string.IsNullOrEmpty(tx.MemberID) ? "Guest" : $"Member ID: {tx.MemberID}";
                Console.WriteLine($"{tx.Timestamp:yyyy-MM-dd HH:mm} | Staff ID: {tx.StaffID} | {memberInfo} | Items: {items} | Total: £{tx.TotalPrice / 100.0:F2}");
            }
        }
    }
}
