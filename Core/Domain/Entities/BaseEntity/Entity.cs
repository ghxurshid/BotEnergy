namespace Domain.Entities.BaseEntity
{ 
    public class Entity
    { 
        public long Id { get; set; }         
        public DateTime CreatedDate { get; set; } = DateTime.Now;          
        public DateTime UpdatedDate { get; set; } = DateTime.Now; 
        public bool IsDeleted { get; set; } = false;
    }
}
