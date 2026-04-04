using Microsoft.AspNetCore.Mvc;
using Salon.Models;

namespace Salon.Controllers
{
    public class EmployeeController : Controller
    {
        private static List<Employee> _employees = new();
        private static int _nextId = 1;

        public IActionResult Index()
        {
            return View(_employees);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Employee employee)
        {
            if (ModelState.IsValid)
            {
                employee.Id = _nextId++;
                _employees.Add(employee);
                return RedirectToAction(nameof(Index));
            }
            return View(employee);
        }

        public IActionResult Edit(int id)
        {
            var employee = _employees.FirstOrDefault(e => e.Id == id);
            if (employee == null) return NotFound();
            return View(employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Employee employee)
        {
            if (id != employee.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var existing = _employees.FirstOrDefault(e => e.Id == id);
                if (existing == null) return NotFound();

                existing.Name = employee.Name;
                existing.FullName = employee.FullName;
                existing.Phone = employee.Phone;
                existing.Nationality = employee.Nationality;
                existing.JobTitle = employee.JobTitle;
                existing.Department = employee.Department;
                existing.Salary = employee.Salary;
                existing.Commission = employee.Commission;
                existing.HireDate = employee.HireDate;
                existing.ResidencyExpiry = employee.ResidencyExpiry;
                existing.IsActive = employee.IsActive;
                existing.ServiceType = employee.ServiceType;

                return RedirectToAction(nameof(Index));
            }
            return View(employee);
        }

        public IActionResult Delete(int id)
        {
            var employee = _employees.FirstOrDefault(e => e.Id == id);
            if (employee == null) return NotFound();
            return View(employee);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var employee = _employees.FirstOrDefault(e => e.Id == id);
            if (employee != null)
                _employees.Remove(employee);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Details(int id)
        {
            var employee = _employees.FirstOrDefault(e => e.Id == id);
            if (employee == null) return NotFound();
            return View(employee);
        }
    }
}
