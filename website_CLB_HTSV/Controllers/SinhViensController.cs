using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using SendGrid.Helpers.Errors.Model;
using website_CLB_HTSV.Data;
using website_CLB_HTSV.Models;

namespace website_CLB_HTSV.Controllers
{
    public class SinhViensController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public SinhViensController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult UpdateProfile()
        {
            if (!User.IsInRole("Administrators")){
                // Lấy thông tin sinh viên đăng nhập hiện tại
                var currentUserId = User.Identity.Name.Split('@')[0];
                // Kiểm tra xem sinh viên đã tồn tại trong cơ sở dữ liệu chưa
                var sinhVien = _context.SinhVien.Find(currentUserId);

                ViewData["MaChucVu"] = new SelectList(_context.ChucVu, "MaChucVu", "TenChucVu", "CV04");
                ViewData["MaLop"] = new SelectList(_context.LopHoc, "MaLop", "MaLop");
                return View(sinhVien);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([Bind("MaSV,HoTen,NgaySinh,DienThoai,Email,MaLop,MaChucVu")] SinhVien updatedSinhVien, IFormFile newImage)
        {
            var currentUserId = User.Identity.Name.Split('@')[0];
            var sinhVien = await GetSinhVienAsync(currentUserId);

            if (sinhVien == null)
            {
                await CreateNewSinhVienAsync(updatedSinhVien, newImage, currentUserId);
            }
            else
            {
                await UpdateExistingSinhVienAsync(sinhVien, updatedSinhVien, newImage);
            }

            return RedirectToAction("Index", "Home");
        }

        private async Task<SinhVien> GetSinhVienAsync(string maSV)
        {
            return await _context.SinhVien.FindAsync(maSV);
        }

        private async Task CreateNewSinhVienAsync(SinhVien updatedSinhVien, IFormFile newImage, string currentUserId)
        {
            var sinhVien = new SinhVien
            {
                MaSV = currentUserId,
                HoTen = updatedSinhVien.HoTen,
                NgaySinh = updatedSinhVien.NgaySinh,
                DienThoai = updatedSinhVien.DienThoai,
                Email = updatedSinhVien.Email,
                MaLop = updatedSinhVien.MaLop,
                MaChucVu = updatedSinhVien.MaChucVu
            };

            await SaveImageAndUpdateSinhVienAsync(sinhVien, newImage);
        }

        private async Task UpdateExistingSinhVienAsync(SinhVien sinhVien, SinhVien updatedSinhVien, IFormFile newImage)
        {
            sinhVien.HoTen = updatedSinhVien.HoTen;
            sinhVien.NgaySinh = updatedSinhVien.NgaySinh;
            sinhVien.DienThoai = updatedSinhVien.DienThoai;
            sinhVien.Email = updatedSinhVien.Email;
            sinhVien.MaLop = updatedSinhVien.MaLop;
            sinhVien.MaChucVu = updatedSinhVien.MaChucVu;

            await SaveImageAndUpdateSinhVienAsync(sinhVien, newImage);
        }

        private async Task SaveImageAndUpdateSinhVienAsync(SinhVien sinhVien, IFormFile newImage)
        {
            if (newImage != null && newImage.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(newImage.FileName);
                var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "userimages", fileName);
                using (var stream = new FileStream(uploadPath, FileMode.Create))
                {
                    await newImage.CopyToAsync(stream);
                }

                sinhVien.HinhAnh = fileName;
            }

            try
            {
                if (_context.Entry(sinhVien).State == EntityState.Detached)
                {
                    _context.Add(sinhVien);
                }
                else
                {
                    _context.Update(sinhVien);
                }

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SinhVienExists(sinhVien.MaSV))
                {
                    throw new NotFoundException(); // Bổ sung custom exception hoặc xử lý khác tùy nhu cầu
                }
                else
                {
                    throw; // Hoặc xử lý lỗi khác
                }
            }
        }







        [Authorize(Roles = "Administrators")]
        // Phương thức xuất Excel
        public IActionResult ExportToExcel()
        {
            // Lấy searchString từ Session
            var searchString = HttpContext.Session.GetString("searchString");

            var sinhViens = from s in _context.SinhVien.Include(s => s.ChucVu).Include(s => s.LopHoc)
                            select s;

            if (!string.IsNullOrEmpty(searchString))
            {
                sinhViens = sinhViens.Where(s => s.MaSV.Contains(searchString)
                                                || s.HoTen.Contains(searchString)
                                                || s.ChucVu.TenChucVu.Contains(searchString)
                                                || s.LopHoc.MaLop.Contains(searchString));
            }
            // Tạo một package Excel mới
            using (var package = new ExcelPackage())
            {
                // Tạo một sheet mới
                var worksheet = package.Workbook.Worksheets.Add("Danh sách sinh viên");

                // Đặt tiêu đề cho các cột
                worksheet.Cells[1, 1].Value = "Mã Sinh Viên";
                worksheet.Cells[1, 2].Value = "Họ Tên";
                worksheet.Cells[1, 3].Value = "Ngày Sinh";
                worksheet.Cells[1, 4].Value = "Điện Thoại";
                worksheet.Cells[1, 5].Value = "Email";
                worksheet.Cells[1, 6].Value = "Mã Lớp";
                worksheet.Cells[1, 7].Value = "Mã Chức Vụ";
                worksheet.Cells[1, 8].Value = "Hình Ảnh";

                // Đặt dữ liệu cho các ô
                int row = 2;
                foreach (var sinhVien in sinhViens)
                {
                    worksheet.Cells[row, 1].Value = sinhVien.MaSV;
                    worksheet.Cells[row, 2].Value = sinhVien.HoTen;
                    worksheet.Cells[row, 3].Value = sinhVien.NgaySinh;
                    worksheet.Cells[row, 4].Value = sinhVien.DienThoai;
                    worksheet.Cells[row, 5].Value = sinhVien.Email;
                    worksheet.Cells[row, 6].Value = sinhVien.MaLop;
                    worksheet.Cells[row, 7].Value = sinhVien.MaChucVu;
                    worksheet.Cells[row, 8].Value = sinhVien.HinhAnh;
                    row++;
                }

                // Tạo một stream để lưu trữ tệp Excel
                MemoryStream stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;
                // Định dạng tên file với ngày tháng năm và thuộc tính được tìm kiếm
                string fileName = $"DanhSachSinhVien_{searchString}_{DateTime.Today:ddMMyyyy}.xlsx";

                // Trả về tệp Excel như một file
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }




        // GET: SinhViens
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Index(string searchString, int? pageNumber)
        {
            var sinhViens = from s in _context.SinhVien.Include(s => s.ChucVu).Include(s => s.LopHoc)
                            select s;

            if (!string.IsNullOrEmpty(searchString))
            {
                sinhViens = sinhViens.Where(s => s.MaSV.Contains(searchString)
                                                || s.HoTen.Contains(searchString)
                                                || s.ChucVu.TenChucVu.Contains(searchString)
                                                || s.LopHoc.MaLop.Contains(searchString));

                // Lưu searchString vào Session
                HttpContext.Session.SetString("searchString", searchString);
            }

            int pageSize = 10; // Số lượng mục trên mỗi trang
            return View(await PaginatedList<SinhVien>.CreateAsync(sinhViens.AsNoTracking(), pageNumber ?? 1, pageSize));
        }

        [Authorize]
        public async Task<IActionResult> BanChuNhiem()
        {
            var applicationDbContext = _context.SinhVien.Include(s => s.ChucVu).Include(s => s.LopHoc).Where(s => s.ChucVu.MaChucVu != "CV04" && s.ChucVu.MaChucVu != null);
            return View(await applicationDbContext.ToListAsync());
        }


        // GET: SinhViens/Details/5

/*        [Authorize]*/
        public async Task<IActionResult> Details(string id)
        {
            if (id == null || _context.SinhVien == null)
            {
                return NotFound();
            }

            var sinhVien = await _context.SinhVien
                .Include(s => s.ChucVu)
                .Include(s => s.LopHoc)
                .FirstOrDefaultAsync(m => m.MaSV == id);
            if (sinhVien == null)
            {
                return NotFound();
            }

            return View(sinhVien);
        }

        // GET: SinhViens/Create
        [Authorize(Roles = "Administrators")]
        public IActionResult Create()
        {
            ViewData["MaChucVu"] = new SelectList(_context.ChucVu, "MaChucVu", "TenChucVu");
            ViewData["MaLop"] = new SelectList(_context.LopHoc, "MaLop", "MaLop");
            return View();
        }

        // POST: SinhViens/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Create([Bind("MaSV,HoTen,NgaySinh,DienThoai,Email,MaLop,MaChucVu")] SinhVien sinhVien, IFormFile HinhAnh)
        {
/*            if (ModelState.IsValid)
            {*/
                if (HinhAnh != null && HinhAnh.Length > 0)
                {
                    // Tạo tên file duy nhất
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(HinhAnh.FileName);

                    // Lưu file vào vị trí chỉ định (thay thế bằng logic của bạn)
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "userimages", fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await HinhAnh.CopyToAsync(stream);
                    }

                    // Cập nhật tên file cho model (tùy chọn)
                    sinhVien.HinhAnh = fileName;
                }

                _context.Add(sinhVien);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            /*}*/

            ViewData["MaChucVu"] = new SelectList(_context.ChucVu, "MaChucVu", "TenChucVu", sinhVien.MaChucVu);
            ViewData["MaLop"] = new SelectList(_context.LopHoc, "MaLop", "TenLop", sinhVien.MaLop);
            return View(sinhVien);
        }



        // GET: SinhViens/Edit/5
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sinhVien = await _context.SinhVien.FindAsync(id);
            if (sinhVien == null)
            {
                return NotFound();
            }

            ViewData["MaLop"] = new SelectList(_context.LopHoc, "MaLop", "TenLop", sinhVien.MaLop);
            ViewData["MaChucVu"] = new SelectList(_context.ChucVu, "MaChucVu", "TenChucVu", sinhVien.MaChucVu);
            return View(sinhVien);
        }

        // POST: SinhViens/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        // POST: SinhViens/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Edit(string id, [Bind("MaSV,HoTen,NgaySinh,DienThoai,Email,MaLop,MaChucVu")] SinhVien sinhVien, IFormFile HinhAnh)
        {
            if (id != sinhVien.MaSV)
            {
                return NotFound();
            }

            // Lấy SinhVien cũ từ cơ sở dữ liệu
            var sinhVienOld = await _context.SinhVien.AsNoTracking().FirstOrDefaultAsync(s => s.MaSV == id);
            if (sinhVienOld == null)
            {
                return NotFound();
            }


                // Kiểm tra nếu có hình ảnh mới được tải lên
                if (HinhAnh != null && HinhAnh.Length > 0)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(HinhAnh.FileName);
                    var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "userimages", fileName);
                    using (var stream = new FileStream(uploadPath, FileMode.Create))
                    {
                        await HinhAnh.CopyToAsync(stream);
                    }
                    sinhVien.HinhAnh = fileName; // Cập nhật với tên file hình ảnh mới
                }
                else
                {
                    sinhVien.HinhAnh = sinhVienOld.HinhAnh; // Giữ nguyên hình ảnh cũ nếu không có hình mới
                }

                try
                {
                    _context.Update(sinhVien);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SinhVienExists(sinhVien.MaSV))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            
            // Load lại thông tin nếu ModelState không hợp lệ
            ViewData["MaChucVu"] = new SelectList(_context.ChucVu, "MaChucVu", "MaChucVu", sinhVien.MaChucVu);
            ViewData["MaLop"] = new SelectList(_context.LopHoc, "MaLop", "MaLop", sinhVien.MaLop);
            return View(sinhVien);
        }


        // GET: SinhViens/Delete/5
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sinhVien = await _context.SinhVien.FindAsync(id);
            if (sinhVien == null)
            {
                return NotFound();
            }

            return View(sinhVien);
        }


        // POST: SinhViens/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var sinhVien = await _context.SinhVien.FindAsync(id);

            // Xử lý xóa file ảnh (nếu có)

            if (sinhVien.HinhAnh != null)
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "userimages", sinhVien.HinhAnh);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.SinhVien.Remove(sinhVien);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SinhVienExists(string id)
        {
          return (_context.SinhVien?.Any(e => e.MaSV == id)).GetValueOrDefault();
        }
    }
}
