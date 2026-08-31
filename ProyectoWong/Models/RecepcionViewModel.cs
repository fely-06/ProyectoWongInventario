using System.ComponentModel.DataAnnotations;

namespace ProyectoWong.Models
{
    // Paso 1-2: RECEIVING + SCAN PO -> crea la cabecera de recepción
    public class RecepcionViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Debe escanear/seleccionar una orden de compra")]
        public int OrdenCompraId { get; set; }

        public int UsuarioId { get; set; } // se asigna desde el usuario logueado

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "EnProceso";

        public DateTime FechaRecepcion { get; set; } = DateTime.Now;
    }
}