﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using website_CLB_HTSV.Data;
using website_CLB_HTSV.Models;

namespace website_CLB_HTSV.Controllers
{
    public class ThamGiaHoatDongsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ThamGiaHoatDongsController(ApplicationDbContext context)
        {
            _context = context;
        }
        //Xuât danh sách

        //Cập nhật minh chứng 
        [HttpGet]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> XuatDS()
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
        [Authorize(Roles = "Administrators")]
        public IActionResult XuatDS(string hoatDongId)
        {
            // Lấy danh sách sinh viên tham gia hoạt động được chọn
            var danhSachSinhVien = from s in _context.ThamGiaHoatDong
                                   .Where(tg => tg.MaHoatDong == hoatDongId)
                                   .Include(t => t.DangKyHoatDong)
                                   .Include(h => h.DangKyHoatDong.HoatDong)
                                   .Include(s => s.DangKyHoatDong.SinhVien)
                                   select s;

            // Tạo một package Excel mới
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial; // Khai báo loại giấy phép
            using (var package = new ExcelPackage())
            {
                // Tạo một Sheet mới
                var worksheet = package.Workbook.Worksheets.Add("Danh sách sinh viên");

                // Đặt các tiêu đề cho các cột
                worksheet.Cells[1, 1].Value = "MSSV";
                worksheet.Cells[1, 2].Value = "Họ và tên";
                worksheet.Cells[1, 3].Value = "Mã lớp";

                // Có thể thêm các tiêu đề khác tùy ý

                // Đặt dữ liệu cho từng hàng
                int row = 2;
                foreach (var sinhVien in danhSachSinhVien)
                {
                    worksheet.Cells[row, 1].Value = sinhVien.MaSV;
                    worksheet.Cells[row, 2].Value = sinhVien.DangKyHoatDong.SinhVien.HoTen;
                    worksheet.Cells[row, 3].Value = sinhVien.DangKyHoatDong.SinhVien.MaLop;

                    // Có thể thêm dữ liệu cho các cột khác tùy ý
                    row++;
                }

                // Lưu trữ tệp Excel vào một MemoryStream
                var stream = new MemoryStream(package.GetAsByteArray());
                string fileName = $"DanhSachSinhVien_{hoatDongId}_{DateTime.Today:ddMMyyyy}.xlsx";
                // Trả về phản hồi file Excel
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }





        // GET: ThamGiaHoatDongs
        // GET: ThamGiaHoatDongs
        public async Task<IActionResult> Index(string searchString)
        {
            IQueryable<ThamGiaHoatDong> danhSachThamGiaHoatDong = _context.ThamGiaHoatDong
                .Include(t => t.DangKyHoatDong)
                .Include(h => h.DangKyHoatDong.HoatDong)
                .Include(s => s.DangKyHoatDong.SinhVien);

            if (!string.IsNullOrEmpty(searchString))
            {
                danhSachThamGiaHoatDong = danhSachThamGiaHoatDong.Where(tg => tg.DangKyHoatDong.HoatDong.MaHoatDong.Contains(searchString) || tg.DangKyHoatDong.HoatDong.TenHoatDong.Contains(searchString));
            }

            if (!User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "Bạn cần đăng nhập để truy cập vào trang này.";
            }
            else
            {
                if (User.IsInRole("Administrators"))
                {
                    return View(await danhSachThamGiaHoatDong.ToListAsync());
                }
                else
                {
                    var mssv = User.Identity.Name.Split('@')[0];
                    danhSachThamGiaHoatDong = danhSachThamGiaHoatDong.Where(h => h.MaSV == mssv);
                    return View(await danhSachThamGiaHoatDong.ToListAsync());
                }
            }
            return Redirect("/Identity/Account/Login");
        }







        // GET: ThamGiaHoatDongs/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null || _context.ThamGiaHoatDong == null)
            {
                return NotFound();
            }

            var thamGiaHoatDong = await _context.ThamGiaHoatDong
                .Include(t => t.DangKyHoatDong)
                .FirstOrDefaultAsync(m => m.MaThamGiaHoatDong == id);
            if (thamGiaHoatDong == null)
            {
                return NotFound();
            }

            return View(thamGiaHoatDong);
        }

        // GET: ThamGiaHoatDongs/Create
        [Authorize(Roles = "Administrators")]
        public IActionResult Create()
        {
            ViewData["MaDangKy"] = new SelectList(_context.DangKyHoatDong, "MaDangKy", "MaDangKy");
            return View();
        }

        // POST: ThamGiaHoatDongs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Create([Bind("MaThamGiaHoatDong,MaDangKy,MaSV,MaHoatDong,DaThamGia,LinkMinhChung")] ThamGiaHoatDong thamGiaHoatDong)
        {
            if (ModelState.IsValid)
            {
                _context.Add(thamGiaHoatDong);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["MaDangKy"] = new SelectList(_context.DangKyHoatDong, "MaDangKy", "MaDangKy", thamGiaHoatDong.MaDangKy);
            return View(thamGiaHoatDong);
        }

        // GET: ThamGiaHoatDongs/Edit/5
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null || _context.ThamGiaHoatDong == null)
            {
                return NotFound();
            }

            var thamGiaHoatDong = await _context.ThamGiaHoatDong.FindAsync(id);
            if (thamGiaHoatDong == null)
            {
                return NotFound();
            }
            ViewData["MaDangKy"] = new SelectList(_context.DangKyHoatDong, "MaDangKy", "MaDangKy", thamGiaHoatDong.MaDangKy);
            return View(thamGiaHoatDong);
        }

        // POST: ThamGiaHoatDongs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Edit(string id, [Bind("MaThamGiaHoatDong,MaDangKy,MaSV,MaHoatDong,DaThamGia,LinkMinhChung")] ThamGiaHoatDong thamGiaHoatDong)
        {
            if (id != thamGiaHoatDong.MaThamGiaHoatDong)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(thamGiaHoatDong);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ThamGiaHoatDongExists(thamGiaHoatDong.MaThamGiaHoatDong))
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
            ViewData["MaDangKy"] = new SelectList(_context.DangKyHoatDong, "MaDangKy", "MaDangKy", thamGiaHoatDong.MaDangKy);
            return View(thamGiaHoatDong);
        }

        // GET: ThamGiaHoatDongs/Delete/5
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null || _context.ThamGiaHoatDong == null)
            {
                return NotFound();
            }

            var thamGiaHoatDong = await _context.ThamGiaHoatDong
                .Include(t => t.DangKyHoatDong)
                .FirstOrDefaultAsync(m => m.MaThamGiaHoatDong == id);
            if (thamGiaHoatDong == null)
            {
                return NotFound();
            }

            return View(thamGiaHoatDong);
        }

        // POST: ThamGiaHoatDongs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            if (_context.ThamGiaHoatDong == null)
            {
                return Problem("Entity set 'ApplicationDbContext.ThamGiaHoatDong'  is null.");
            }
            var thamGiaHoatDong = await _context.ThamGiaHoatDong.FindAsync(id);


            if (thamGiaHoatDong != null)
            {
                _context.ThamGiaHoatDong.Remove(thamGiaHoatDong);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ThamGiaHoatDongExists(string id)
        {
          return (_context.ThamGiaHoatDong?.Any(e => e.MaThamGiaHoatDong == id)).GetValueOrDefault();
        }
    }
}
