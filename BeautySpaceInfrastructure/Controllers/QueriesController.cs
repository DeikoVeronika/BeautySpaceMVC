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
            ViewBag.Categories = new SelectList(_context.Categories.OrderBy(c => c.Name), "Id", "Name");

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
            var employeeNames = await _context.Employees.Select(e => e.FirstName).Distinct().OrderBy(name => name).ToListAsync();

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
            ViewBag.Positions = new SelectList(_context.Positions.OrderBy(p => p.Name), "Id", "Name");

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



        // Запит 4: Повернути всіх клієнтів, у яких загальна сума їхніх бронювань перевищує задану величину (три таблиці: Reservations, Clients та Services)?
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


        // Запит 5: Знайти всіх працівників які надають задану кількість послуг 
        public async Task<IActionResult> Query5(int? serviceCount)
        {
            if (serviceCount == null)
            {
                return View(new List<Employee>());
            }

            var employees = await _context.Employees
                .FromSqlInterpolated($@"
            SELECT e.Id, e.FirstName, e.LastName, e.PositionId, e.EmployeePortrait, e.PhoneNumber
            FROM Employees e
            JOIN EmployeeServices es ON e.Id = es.EmployeeID
            GROUP BY e.Id, e.FirstName, e.LastName, e.PositionId, e.EmployeePortrait, e.PhoneNumber
            HAVING COUNT(es.ServiceID) = {serviceCount}
        ")
                .ToListAsync();

            if (!employees.Any())
            {
                ViewBag.Message = "Працівників, які надають задану кількість послуг, не знайдено.";
            }

            return View(employees);
        }










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



        /*-------------*/
        //Запит 6: Знаходження імен всіх працівників, які надають точно такі ж послуги як і обраний працівник
        public async Task<IActionResult> Query6(int? employeeId)
        {
            ViewBag.Employees = new SelectList(_context.Employees.OrderBy(c => c.LastName), "Id", "FullName");

            if (employeeId == null)
            {
                return View(new List<Employee>());
            }

            var employees = await _context.Employees
                .FromSqlInterpolated($@"
                SELECT e2.*
                FROM Employees e2
                WHERE e2.Id IN (
                    SELECT es2.EmployeeId
                    FROM EmployeeServices es2
                    GROUP BY es2.EmployeeId
                    HAVING COUNT(DISTINCT es2.ServiceId) = (
                        SELECT COUNT(DISTINCT es1.ServiceId)
                        FROM EmployeeServices es1
                        WHERE es1.EmployeeId = {employeeId}
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM EmployeeServices es1
                        WHERE es1.EmployeeId = {employeeId}
                        AND NOT EXISTS (
                            SELECT 1
                            FROM EmployeeServices es2_inner
                            WHERE es2_inner.EmployeeId = es2.EmployeeId
                            AND es2_inner.ServiceId = es1.ServiceId
                        )
                    )
                    AND es2.EmployeeId != {employeeId}
                )
                ").ToListAsync();

            if (!employees.Any())
            {
                ViewBag.Message = "Працівники, які надають точно такі ж послуги, не знайдено.";
            }

            return View(employees);
        }



        // Запит 7: Знаходження імен всіх клієнтів, у яких кількість бронювань така сама, як і у обраного клієнта
        public async Task<IActionResult> Query7(int? clientId)
        {
            ViewBag.Clients = new SelectList(_context.Clients.OrderBy(c => c.LastName), "Id", "FullName");

            if (clientId == null)
            {
                return View(new List<Client>());
            }

            var clients = await _context.Clients
                .FromSqlInterpolated($@"
                SELECT c2.*
                FROM Clients c2
                JOIN Reservations r2 ON c2.Id = r2.ClientId
                GROUP BY c2.Id, c2.FirstName, c2.LastName, c2.PhoneNumber, c2.Birthday, c2.Email
                HAVING COUNT(r2.Id) = 
                (
                    SELECT COUNT(r3.Id)
                    FROM Reservations r3
                    WHERE r3.ClientId = {clientId}
                )
                AND c2.Id != {clientId}
            ").ToListAsync();

            if (!clients.Any())
            {
                ViewBag.Message = "Клієнти з такою ж кількістю бронювань не знайдено.";
            }

            return View(clients);
        }

        // Запит 8: Знайти клієнтів, у яких усі бронювання створені тільки до тих самих працівників як і у обраного клієнта
        public async Task<IActionResult> Query8(int? clientId)
        {
            ViewBag.Clients = new SelectList(_context.Clients.OrderBy(c => c.LastName), "Id", "FullName");

            if (clientId == null)
            {
                return View(new List<Client>());
            }

            var clients = await _context.Clients
                .FromSqlInterpolated($@"
                    SELECT c2.*
                    FROM Clients c2
                    WHERE NOT EXISTS 
                    (
                        SELECT es1.EmployeeId
                        FROM Reservations r1
                        JOIN TimeSlots ts1 ON r1.TimeSlotId = ts1.Id
                        JOIN EmployeeServices es1 ON ts1.EmployeeServiceId = es1.Id
                        WHERE r1.ClientId = {clientId}
                        EXCEPT
                        SELECT es2.EmployeeId
                        FROM Reservations r2
                        JOIN TimeSlots ts2 ON r2.TimeSlotId = ts2.Id
                        JOIN EmployeeServices es2 ON ts2.EmployeeServiceId = es2.Id
                        WHERE r2.ClientId = c2.Id
                    )
                    AND NOT EXISTS 
                    (
                        SELECT es3.EmployeeId
                        FROM Reservations r3
                        JOIN TimeSlots ts3 ON r3.TimeSlotId = ts3.Id
                        JOIN EmployeeServices es3 ON ts3.EmployeeServiceId = es3.Id
                        WHERE r3.ClientId = c2.Id
                        EXCEPT
                        SELECT es4.EmployeeId
                        FROM Reservations r4
                        JOIN TimeSlots ts4 ON r4.TimeSlotId = ts4.Id
                        JOIN EmployeeServices es4 ON ts4.EmployeeServiceId = es4.Id
                        WHERE r4.ClientId = {clientId}
                    )
                    AND c2.Id != {clientId}
                    ").ToListAsync();

            if (!clients.Any())
            {
                ViewBag.Message = "Клієнти, у яких усі бронювання тільки до тих самих працівників, не знайдено.";
            }

            return View(clients);
        }


    }
}