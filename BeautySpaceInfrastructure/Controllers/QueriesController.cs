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
                return View(new List<Service>());
            }

            var services = await _context.Services
                .FromSqlInterpolated($@"SELECT * FROM Services WHERE CategoryId = {categoryId} AND Price > {minPrice}")
                .ToListAsync();

            var categoryName = _context.Categories.Find(categoryId.Value)?.Name;

            SetServiceMessage(services, categoryName, minPrice.Value);

            return View(services);
        }



        //Запит 2: Знайти клієнтів, які створили хоча б одне бронювання у працівника з обраним іменем 
        public async Task<IActionResult> Query2(string employeeFirstName)
        {
            var employeeNames = await GetEmployeeNamesAsync();

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

            SetClientMessage(clients, employeeFirstName);

            return View(clients);
        }

        // Запит 3: Пошук таймслотів за обраною категорією
        public async Task<IActionResult> Query3(int categoryId)
        {
            var categoriesWithTimeSlots = await GetCategoriesWithTimeSlotsAsync();

            ViewBag.Categories = new SelectList(categoriesWithTimeSlots, "Id", "Name");

            var timeSlots = await _context.TimeSlots
                .FromSqlInterpolated($@"
        SELECT ts.*
        FROM TimeSlots ts
        JOIN EmployeeServices es ON ts.EmployeeServiceId = es.Id
        JOIN Services s ON es.ServiceId = s.Id
        WHERE s.CategoryId = {categoryId}")
                .ToListAsync();

            return View(timeSlots);
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
            HAVING SUM(s.Price) > {totalReservationCost}")
                .ToListAsync();

            SetClientMessage(clients, totalReservationCost);

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
            HAVING COUNT(es.ServiceID) = {serviceCount}")
                .ToListAsync();

            SetEmployeeMessage(employees, serviceCount);

            return View(employees);
        }

        //Запит 6: Знаходження імен всіх працівників, які надають точно такі ж послуги як і обраний працівник
        public async Task<IActionResult> Query6(int? employeeId)
        {
            var employeesWithReservations = await GetEmployeesWithReservationsAsync();

            ViewBag.Employees = new SelectList(employeesWithReservations, "Id", "FullName");

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

            var employeeName = _context.Employees.Find(employeeId.Value)?.FirstName;

            if (!employees.Any())
            {
                ViewBag.Message = $"Працівників, які надають точно такі ж послуги як і працівник {employeeName}, не знайдено.";
            }
            else
            {
                ViewBag.Message = $"Працівники, які надають точно такі ж послуги як і працівник {employeeName}:";
            }

            return View(employees);
        }

        // Запит 7: Знаходження імен всіх клієнтів, у яких кількість бронювань така сама, як і у обраного клієнта
        public async Task<IActionResult> Query7(int? clientId)
        {
            var clientsWithReservations = await GetClientsWithReservationsAsync();

            ViewBag.Clients = new SelectList(clientsWithReservations, "Id", "FullName");

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

            var clientName = _context.Clients.Find(clientId.Value)?.FirstName;

            ViewBag.Message = SetMessageForClients(clients, clientName);

            return View(clients);
        }

        // Запит 8: Знайти клієнтів, у яких усі бронювання створені тільки до тих самих працівників як і у обраного клієнта
        public async Task<IActionResult> Query8(int? clientId)
        {
            var clientsWithReservations = await GetClientsWithReservationsAsync();
            ViewBag.Clients = new SelectList(clientsWithReservations, "Id", "FullName");

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

            var clientName = _context.Clients.Find(clientId.Value)?.FirstName;

            ViewBag.Message = SetMessageForClientsIdenticalReservation(clients, clientName);

            return View(clients);
        }


        private async Task<List<string>> GetEmployeeNamesAsync()
        {
            return await _context.Employees
                .Where(e => _context.EmployeeServices
                    .Any(es => es.EmployeeId == e.Id &&
                               _context.TimeSlots
                                   .Any(ts => ts.EmployeeServiceId == es.Id &&
                                              _context.Reservations.Any(r => r.TimeSlotId == ts.Id))))
                .Select(e => e.FirstName)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();
        }

        private async Task<List<Category>> GetCategoriesWithTimeSlotsAsync()
        {
            return await _context.Categories
                .Where(c => _context.Services
                    .Where(s => s.CategoryId == c.Id)
                    .Any(s => _context.EmployeeServices
                        .Where(es => es.ServiceId == s.Id)
                        .Any(es => _context.TimeSlots.Any(ts => ts.EmployeeServiceId == es.Id))))
                .ToListAsync();
        }

        private async Task<List<Employee>> GetEmployeesWithReservationsAsync()
        {
            return await _context.Employees
                .Where(e => _context.Reservations
                    .Join(_context.TimeSlots, r => r.TimeSlotId, ts => ts.Id, (r, ts) => new { r, ts })
                    .Any(rt => rt.ts.EmployeeService.EmployeeId == e.Id))
                .OrderBy(e => e.LastName)
                .ToListAsync();
        }

        private async Task<List<Client>> GetClientsWithReservationsAsync()
        {
            return await _context.Clients
                .Where(c => _context.Reservations.Any(r => r.ClientId == c.Id))
                .OrderBy(c => c.LastName)
                .ToListAsync();
        }

        private void SetServiceMessage(IEnumerable<Service> services, string categoryName, decimal minPrice)
        {
            if (!services.Any())
            {
                ViewBag.Message = $"Послуг з обраною категорією {categoryName} та вартістю вище {minPrice} грн не знайдено.";
            }
            else
            {
                ViewBag.Message = $"Послуги обраної категорії {categoryName}, вартість яких більша за {minPrice} грн:";
            }
        }

        private void SetClientMessage(IEnumerable<Client> clients, string employeeFirstName)
        {
            if (!clients.Any())
            {
                ViewBag.Message = $"Клієнти, які створили бронювання у працівника з ім'ям {employeeFirstName}, не знайдені.";
            }
            else
            {
                ViewBag.Message = $"Клієнти, які створили бронювання у працівника з ім'ям {employeeFirstName}:";
            }
        }

        private void SetClientMessage(IEnumerable<Client> clients, decimal? totalReservationCost)
        {
            if (!clients.Any())
            {
                ViewBag.Message = $"Клієнтів, у яких загальна сума бронювань перевищує {totalReservationCost} грн, не знайдено.";
            }
            else
            {
                ViewBag.Message = $"Клієнти, у яких загальна сума бронювань перевищує {totalReservationCost} грн:";
            }
        }

        private void SetEmployeeMessage(IEnumerable<Employee> employees, int? serviceCount)
        {
            if (!employees.Any())
            {
                ViewBag.Message = $"Працівників, які надають задану кількість послуг ({serviceCount}), не знайдено.";
            }
            else
            {
                ViewBag.Message = $"Працівники, які надають задану кількість послуг ({serviceCount}):";
            }
        }
        private string SetMessageForClients(List<Client> clients, string clientName)
        {
            if (!clients.Any())
            {
                return $"Клієнтів з такою ж кількістю бронювань як у клієнта {clientName} не знайдено.";
            }
            else
            {
                return $"Клієнти з такою ж кількістю бронювань як у клієнта {clientName}:";
            }
        }

        private string SetMessageForClientsIdenticalReservation(List<Client> clients, string clientName)
        {
            if (!clients.Any())
            {
                return $"Клієнтів, у яких усі бронювання тільки до тих самих працівників, як у клієнта {clientName}, не знайдено.";
            }
            else
            {
                return $"Клієнти, у яких усі бронювання тільки до тих самих працівників, як у клієнта {clientName}:";
            }
        }


    }
}