namespace Points.Models.DbModels
{
    public class ScCardStepDbModel
    {
        public int ScCardStepID { get; set; }
        public int ScCardID { get; set; }
        public int Order { get; set; }
        public string Title { get; set; } = "";
        public double StepValue { get; set; }
    }

}
