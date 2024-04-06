﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using website_CLB_HTSV.Data;
using website_CLB_HTSV.Models;
using OfficeOpenXml;
namespace website_CLB_HTSV.Controllers
{
    public class DangKyHoatDongsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        public DangKyHoatDongsController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        //Cập nhật minh chứng 
        [HttpGet]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> CapNhatMinhChung()
        {
            var activityList = await _context.HoatDong
                                             .Select(a => new SelectListItem
                                             {
                                                 Value = a.MaHoatDong.ToString(),
                                                 Text = a.TenHoatDong
                                             })
                                             .ToListAsync();

            ViewBag.ActivityList = activityList;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> CapNhatMinhChung(string hoatDongId, string minhChungLink)
        {
            if (string.IsNullOrEmpty(hoatDongId) || string.IsNullOrEmpty(minhChungLink))
            {
                TempData["ErrorMessage"] = "Thông tin hoặc đường dẫn minh chứng không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            // Lấy danh sách sinh viên đã tham gia hoạt động
            var registeredStudents = await _context.ThamGiaHoatDong
                .Where(dk => dk.MaHoatDong == hoatDongId)
                .ToListAsync();


            if (registeredStudents == null || !registeredStudents.Any())
            {
                TempData["ErrorMessage"] = "Không có sinh viên nào đã đăng ký hoạt động này.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var registeredStudent in registeredStudents)
            {
                 registeredStudent.LinkMinhChung = minhChungLink;

                _context.ThamGiaHoatDong.Update(registeredStudent);
            }
            // Cập nhật trạng thái hoạt động nếu link minh chứng không rỗng


            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật minh chứng cho tất cả sinh viên thành công.";
            return RedirectToAction("Index", "ThamGiaHoatDongs");
        }




        //Import
        [HttpGet]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> ImportDiemDanh()
        {
            var activityList = await _context.HoatDong
                                             .Select(a => new SelectListItem
                                             {
                                                 Value = a.MaHoatDong.ToString(),
                                                 Text = a.TenHoatDong
                                             })
                                             .ToListAsync();

            ViewBag.ActivityList = activityList;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> ImportDiemDanh(IFormFile file, string hoatDongId)
        {
            if (file == null || string.IsNullOrEmpty(hoatDongId))
            {
                TempData["ErrorMessage"] = "File hoặc hoạt động không hợp lệ.";
                return RedirectToAction(nameof(ImportDiemDanh)); // Chuyển hướng trở lại action ImportDiemDanh
            }

            var importedStudentCodes = ReadStudentCodesFromExcel(file);

            if (importedStudentCodes == null || !importedStudentCodes.Any())
            {
                TempData["ErrorMessage"] = "Không có dữ liệu trong file.";
                return RedirectToAction(nameof(ImportDiemDanh)); // Chuyển hướng trở lại action ImportDiemDanh
            }

            // Lấy danh sách sinh viên đã đăng ký cho hoạt động
            var registeredStudents = await _context.DangKyHoatDong
                                                .Where(dk => dk.MaHoatDong == hoatDongId)
                                                .ToListAsync();
            var hoatDong = await _context.HoatDong.FirstOrDefaultAsync(h => h.MaHoatDong == hoatDongId);
            foreach (var registeredStudent in registeredStudents)
            {
                if (importedStudentCodes.Contains(registeredStudent.MaSV))
                {
                    // Kiểm tra xem sinh viên đã điểm danh chưa, nếu chưa thì cập nhật
                    var attendanceRecord = await _context.ThamGiaHoatDong
                                                        .FirstOrDefaultAsync(t => t.MaDangKy == registeredStudent.MaDangKy);

                    if (attendanceRecord == null)
                    {
                        var newAttendanceRecord = new ThamGiaHoatDong
                        {
                            MaThamGiaHoatDong = "TG" + DateTime.Now.ToString("yyyyMMddHHmmssfff"),
                            MaDangKy = registeredStudent.MaDangKy,
                            DaThamGia = true,
                            MaSV = registeredStudent.MaSV,
                            MaHoatDong = registeredStudent.MaHoatDong
                        };
                        hoatDong.TrangThai = "Đã kết thúc"; // Cập nhật trạng thái hoạt động là true
                        _context.HoatDong.Update(hoatDong);

                        _context.ThamGiaHoatDong.Add(newAttendanceRecord);
                    }
                }   
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã import và cập nhật điểm danh thành công.";
            return RedirectToAction("Index", "ThamGiaHoatDongs"); // Hoặc trả về một View cụ thể
        }

        private List<string> ReadStudentCodesFromExcel(IFormFile file)
        {
            List<string> studentCodes = new List<string>();
            using (var stream = new MemoryStream())
            {
                file.CopyTo(stream);
                using (var package = new ExcelPackage(stream))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                    int rowCount = worksheet.Dimension.Rows;
                    for (int row = 2; row <= rowCount; row++)
                    {
                        studentCodes.Add(worksheet.Cells[row, 1].Value?.ToString().Trim());
                    }
                }
            }
            return studentCodes;
        }




        [HttpPost]
        [Authorize]
        public async Task<IActionResult> HuyDangKy(string hoatDongId)
        {
            var mssv = User.Identity.Name.Split('@')[0];

            // Tìm mục DangKyHoatDong tương ứng với hoatDongId và mssv và trạng thái đăng ký là true
            var dangKyHoatDong = await _context.DangKyHoatDong
                .FirstOrDefaultAsync(dk => dk.MaHoatDong == hoatDongId
                                        && dk.MaSV == mssv
                                        && dk.TrangThaiDangKy == true);

            if (dangKyHoatDong != null)
            {
                
                try
                {
                    // Xóa mục DangKyHoatDong từ cơ sở dữ liệu
                    _context.DangKyHoatDong.Remove(dangKyHoatDong);
                    await _context.SaveChangesAsync();

                    // Điều hướng người dùng về trang cần thiết hoặc thông báo thành công
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    // Xử lý ngoại lệ nếu có
                    // Ở đây, bạn có thể trả về một thông báo lỗi hoặc điều hướng người dùng đến một trang lỗi
                    return RedirectToAction(nameof(Error));
                }
            }
            else
            {
                // Trả về NotFound nếu không tìm thấy mục tương ứng
                return NotFound();
            }
        }

        private object Error()
        {
            throw new NotImplementedException();
        }







        // GET: DangKyHoatDongs
        [Authorize]
        public async Task<IActionResult> Index(string searchString)
        {
            // Kiểm tra người dùng đã đăng nhập hay chưa
            if (!User.Identity.IsAuthenticated)
            {
                // Nếu chưa đăng nhập, đặt thông báo vào TempData
                TempData["ErrorMessage"] = "Bạn cần đăng nhập để truy cập vào trang này.";
            }
            else
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var Mssv = User.Identity.Name.Split('@')[0];
                if (!User.IsInRole("Administrators"))
                {
                    // Lấy thông tin người dùng hiện tại


                    var hoatdong = _context.DangKyHoatDong.Include(s => s.HoatDong).Include(s => s.SinhVien).Where(dk => dk.MaSV == Mssv && dk.TrangThaiDangKy == true);
                    if (!string.IsNullOrEmpty(searchString))
                    {
                        hoatdong = hoatdong.Where(s => s.MaHoatDong.Contains(searchString) || s.MaSV.Contains(searchString));
                    }
                    return View(await hoatdong.ToListAsync());
                }
                else
                {
                    var hoatdong = _context.DangKyHoatDong.Include(s => s.HoatDong).Include(s => s.SinhVien).Where(dk => dk.TrangThaiDangKy == true);
                    if (!string.IsNullOrEmpty(searchString))
                    {
                        hoatdong = hoatdong.Where(s => s.MaHoatDong.Contains(searchString) || s.MaSV.Contains(searchString));
                    }
                    return View(await hoatdong.ToListAsync());
                }


            } 
            return Redirect("/Identity/Account/Login");
        }

        // GET: DangKyHoatDongs/Details/5
        [Authorize]
        public async Task<IActionResult> Details(string id)
        {
            if (id == null || _context.DangKyHoatDong == null)
            {
                return NotFound();
            }

            var dangKyHoatDong = await _context.DangKyHoatDong
                .Include(d => d.HoatDong)
                .Include(d => d.SinhVien)
                .FirstOrDefaultAsync(m => m.MaDangKy == id);
            if (dangKyHoatDong == null)
            {
                return NotFound();
            }

            return View(dangKyHoatDong);
        }

        // GET: DangKyHoatDongs/Create
        [Authorize(Roles = "Administrators")]
        public IActionResult Create()
        {
            ViewData["MaHoatDong"] = new SelectList(_context.HoatDong, "MaHoatDong", "MaHoatDong");
            ViewData["MaSV"] = new SelectList(_context.SinhVien, "MaSV", "MaSV");
            return View();
        }

        // POST: DangKyHoatDongs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Create([Bind("MaDangKy,MaSV,MaHoatDong,NgayDangKy,TrangThaiDangKy")] DangKyHoatDong dangKyHoatDong)
        {
            if (ModelState.IsValid)
            {
                _context.Add(dangKyHoatDong);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["MaHoatDong"] = new SelectList(_context.HoatDong, "MaHoatDong", "MaHoatDong", dangKyHoatDong.MaHoatDong);
            ViewData["MaSV"] = new SelectList(_context.SinhVien, "MaSV", "MaSV", dangKyHoatDong.MaSV);
            return View(dangKyHoatDong);
        }

        // GET: DangKyHoatDongs/Edit/5
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null || _context.DangKyHoatDong == null)
            {
                return NotFound();
            }

            var dangKyHoatDong = await _context.DangKyHoatDong.FindAsync(id);
            if (dangKyHoatDong == null)
            {
                return NotFound();
            }
            ViewData["MaHoatDong"] = new SelectList(_context.HoatDong, "MaHoatDong", "MaHoatDong", dangKyHoatDong.MaHoatDong);
            ViewData["MaSV"] = new SelectList(_context.SinhVien, "MaSV", "MaSV", dangKyHoatDong.MaSV);
            return View(dangKyHoatDong);
        }

        // POST: DangKyHoatDongs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Edit(string id, [Bind("MaDangKy,MaSV,MaHoatDong,NgayDangKy,TrangThaiDangKy")] DangKyHoatDong dangKyHoatDong)
        {
            if (id != dangKyHoatDong.MaDangKy)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(dangKyHoatDong);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DangKyHoatDongExists(dangKyHoatDong.MaDangKy))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["MaHoatDong"] = new SelectList(_context.HoatDong, "MaHoatDong", "MaHoatDong", dangKyHoatDong.MaHoatDong);
            ViewData["MaSV"] = new SelectList(_context.SinhVien, "MaSV", "MaSV", dangKyHoatDong.MaSV);
            return View(dangKyHoatDong);
        }

        // GET: DangKyHoatDongs/Delete/5
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null || _context.DangKyHoatDong == null)
            {
                return NotFound();
            }

            var dangKyHoatDong = await _context.DangKyHoatDong
                .Include(d => d.HoatDong)
                .Include(d => d.SinhVien)
                .FirstOrDefaultAsync(m => m.MaDangKy == id);
            if (dangKyHoatDong == null)
            {
                return NotFound();
            }

            return View(dangKyHoatDong);
        }

        // POST: DangKyHoatDongs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            if (_context.DangKyHoatDong == null)
            {
                return Problem("Entity set 'ApplicationDbContext.DangKyHoatDong'  is null.");
            }
            var dangKyHoatDong = await _context.DangKyHoatDong.FindAsync(id);
            if (dangKyHoatDong != null)
            {
                _context.DangKyHoatDong.Remove(dangKyHoatDong);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DangKyHoatDongExists(string id)
        {
          return (_context.DangKyHoatDong?.Any(e => e.MaDangKy == id)).GetValueOrDefault();
        }
    }
}
