
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoWong.Models.Produccion;
using ProyectoWong.Data;

public class OrdenProduccionsController : Controller
{
    private readonly ApplicationDbContext _context;

    public OrdenProduccionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: ORDENPRODUCCIONS
    public async Task<IActionResult> Index()    
    {
        return View(await _context.OrdenProduccion.ToListAsync());
    }

    // GET: ORDENPRODUCCIONS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var ordenproduccion = await _context.OrdenProduccion
            .FirstOrDefaultAsync(m => m.Id == id);
        if (ordenproduccion == null)
        {
            return NotFound();
        }

        return View(ordenproduccion);
    }

    // GET: ORDENPRODUCCIONS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: ORDENPRODUCCIONS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,NumeroOP,ProductoId,Producto,CantidadAProducir,Estado,OrdenCompraId,OrdenCompra,FechaCreacion,FechaInicio,FechaFin,Detalles")] OrdenProduccion ordenproduccion)
    {
        if (ModelState.IsValid)
        {
            _context.Add(ordenproduccion);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(ordenproduccion);
    }

    // GET: ORDENPRODUCCIONS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var ordenproduccion = await _context.OrdenProduccion.FindAsync(id);
        if (ordenproduccion == null)
        {
            return NotFound();
        }
        return View(ordenproduccion);
    }

    // POST: ORDENPRODUCCIONS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,NumeroOP,ProductoId,Producto,CantidadAProducir,Estado,OrdenCompraId,OrdenCompra,FechaCreacion,FechaInicio,FechaFin,Detalles")] OrdenProduccion ordenproduccion)
    {
        if (id != ordenproduccion.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(ordenproduccion);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OrdenProduccionExists(ordenproduccion.Id))
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
        return View(ordenproduccion);
    }

    // GET: ORDENPRODUCCIONS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var ordenproduccion = await _context.OrdenProduccion
            .FirstOrDefaultAsync(m => m.Id == id);
        if (ordenproduccion == null)
        {
            return NotFound();
        }

        return View(ordenproduccion);
    }

    // POST: ORDENPRODUCCIONS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var ordenproduccion = await _context.OrdenProduccion.FindAsync(id);
        if (ordenproduccion != null)
        {
            _context.OrdenProduccion.Remove(ordenproduccion);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool OrdenProduccionExists(int? id)
    {
        return _context.OrdenProduccion.Any(e => e.Id == id);
    }
}
