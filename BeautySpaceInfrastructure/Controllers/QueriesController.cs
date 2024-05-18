using BeautySpaceDomain.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BeautySpaceInfrastructure.Controllers
{
    public class QueriesController : Controller
    {
        private readonly DbbeautySpaceContext _context;
        private readonly string _connectionString;

        public QueriesController(DbbeautySpaceContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        //Запит 1: Повернути всі послуги обраної категорії, вартість яких більша за вказану суму (дві таблиці: Services та Categories)
        public async Task<IActionResult> Query1(int? categoryId, decimal? minPrice)
        {
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");

            if (categoryId == null || minPrice == null)
            {
                // Повернути порожню модель, щоб користувач міг ввести значення
                return View(new List<Service>());
            }

            //var services = await _context.Services
            //    .FromSqlRaw("SELECT * FROM Services WHERE CategoryId = {0} AND Price > {1}", categoryId, minPrice)
            //    .ToListAsync();

            var services = await _context.Services
                .FromSqlInterpolated($@"SELECT * FROM Services WHERE CategoryId = {categoryId} AND Price > {minPrice}")
                .ToListAsync();

            if (!services.Any())
            {
                ViewBag.Message = "Послуги з обраною категорією та вартістю вище вказаної суми не знайдено.";
            }

            return View(services);
        }


        //Запит 2: Знайти клієнтів, які створили хоча б одне бронювання у працівника з обраним іменем (складний?)
        public async Task<IActionResult> Query2(string employeeFirstName)
        {
            var employeeNames = await _context.Employees.Select(e => e.FirstName).Distinct().ToListAsync();

            ViewBag.EmployeeNames = new SelectList(employeeNames);

            if (string.IsNullOrEmpty(employeeFirstName))
            {
                return View(new List<Client>());
            }

            var clients = await _context.Clients
                .FromSqlInterpolated($@"
            SELECT DISTINCT c.*
            FROM Clients c
            JOIN Reservations r ON r.ClientId = c.Id
            JOIN TimeSlots ts ON r.TimeSlotId = ts.Id
            JOIN EmployeeServices es ON ts.EmployeeServiceId = es.Id
            JOIN Employees e ON es.EmployeeId = e.Id
            WHERE e.FirstName = {employeeFirstName}")
                .ToListAsync();

            if (!clients.Any())
            {
                ViewBag.Message = $"Клієнти, які створили бронювання у працівника з ім'ям {employeeFirstName}, не знайдені.";
            }

            return View(clients);
        }

        //Запит 3: Знайти назви та вартість всіх послуг усіх працівників із певної посади
        public async Task<IActionResult> Query3(int? positionId)
        {
            ViewBag.Positions = new SelectList(_context.Positions, "Id", "Name");

            if (positionId == null)
            {
                return View(new List<Service>());
            }

            var services = await _context.Services
                .FromSqlInterpolated($@"SELECT s.* FROM Services s
                                JOIN EmployeeServices es ON s.Id = es.ServiceId
                                JOIN Employees e ON es.EmployeeId = e.Id
                                WHERE e.PositionId = {positionId}")
                .ToListAsync();

            if (!services.Any())
            {
                ViewBag.Message = "Послуги співробітників з обраною посадою не знайдено.";
            }

            return View(services);
        }



        // Запит 4: Повернути всіх клієнтів, у яких загальна сума їх бронювань перевищує задану величину (три таблиці: Reservations, Clients та Services)?
        public async Task<IActionResult> Query4(decimal? totalReservationCost)
        {
            if (totalReservationCost == null)
            {
                return View(new List<Client>());
            }

            var clients = await _context.Clients
                .FromSqlInterpolated($@"
            SELECT c.Id, c.FirstName, c.LastName, c.PhoneNumber, c.Birthday, c.Email
            FROM Clients c
            JOIN Reservations r ON c.Id = r.ClientId
            JOIN TimeSlots ts ON r.TimeSlotId = ts.Id
            JOIN EmployeeServices es ON ts.EmployeeServiceId = es.Id
            JOIN Services s ON es.ServiceID = s.Id
            GROUP BY c.Id, c.FirstName, c.LastName, c.PhoneNumber, c.Birthday, c.Email
            HAVING SUM(s.Price) > {totalReservationCost}
        ")
                .ToListAsync();

            if (!clients.Any())
            {
                ViewBag.Message = "Клієнтів, у яких загальна сума бронювань перевищує задану величину, не знайдено.";
            }

            return View(clients);
        }


        /*-----------------------------------*/

        /*-----------------------------------*/




        //Запит 4 : кількість бронювань, які здійснив конкретний клієнт у конкретного працівника. (чотири таблиці: Reservations, Clients, EmployeeServices та Employees)


        /*
         Reservations: Ця таблиця містить інформацію про резервації. Вона має такі атрибути: Id, ClientId, Info, TimeSlotId.
        Clients: Ця таблиця містить інформацію про клієнтів. Вона має такі атрибути: Id, FirstName, LastName, PhoneNumber, Birthday, Email.
        Positions: Ця таблиця містить інформацію про посади. Вона має такі атрибути: Id, Name.
        Employees: Ця таблиця містить інформацію про співробітників. Вона має такі атрибути: Id, FirstName, LastName, PositionId, EmployeePortrait, PhoneNumber.
        TimeSlots: Ця таблиця містить інформацію про часові слоти. Вона має такі атрибути: Id, EmployeeServiceId, Date, StartTime, EndTime, IsBooked.
        EmployeeServices: Ця таблиця містить інформацію про послуги, які надають співробітники. Вона має такі атрибути: Id, ServiceID, EmployeeID.
        Services: Ця таблиця містить інформацію про послуги. Вона має такі атрибути: Id, Name, Description, Price, CategoryID.
        Categories: Ця таблиця містить інформацію про категорії послуг. Вона має такі атрибути: Id, Name.
        Зв’язки між цими таблицями встановлені за допомогою ліній, які їх з’єднують, що вказує на обмеження зовнішніх ключів. Схема, схоже, призначена для системи резервування або бронювання призначень, де клієнти можуть бронювати часові слоти для різних послуг, які надають співробітники, які мають конкретні ролі або посади.*/


        //Запит на отримання всіх бронювань конкретного клієнта: Використовуються дві таблиці - Reservations та TimeSlots.
        //Запит на отримання всіх послуг, які надає конкретний співробітник: Використовуються дві таблиці - Services та EmployeeServices.
        //Запит на отримання імені та прізвища співробітника, який надає конкретну послугу: Використовуються три таблиці - Employees, EmployeeServices та Services.
        //Запит на отримання всіх бронювань на конкретну дату: Використовуються три таблиці - Reservations, Clients та TimeSlots.

        //Параметризован запити

        //Запит 1: Знаходження імен всіх працівників, які надають точно такі ж послуги, як і працівник E

        //Запит 2: Знаходження імен всіх клієнтів, у яких кількість бронювань така сама, як і у клієнта C

    }
}