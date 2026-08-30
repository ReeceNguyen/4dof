namespace MobusTCP.Models;

public class TrajectoryPoint
{
    public double Time { get; set; } // seconds
    
    // Joint positions (degrees)
    public double Q1 { get; set; }
    public double Q2 { get; set; }
    public double Q3 { get; set; }
    public double Q4 { get; set; }
    
    // Joint velocities (deg/s)
    public double Q1Dot { get; set; }
    public double Q2Dot { get; set; }
    public double Q3Dot { get; set; }
    public double Q4Dot { get; set; }
    
    // Joint accelerations (deg/s^2)
    public double Q1Ddot { get; set; }
    public double Q2Ddot { get; set; }
    public double Q3Ddot { get; set; }
    public double Q4Ddot { get; set; }
    
    // Cartesian positions (mm) & Pitch (deg)
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double Pitch { get; set; }
    
    // Dynamic torques (N*m)
    public double Tau1 { get; set; }
    public double Tau2 { get; set; }
    public double Tau3 { get; set; }
    public double Tau4 { get; set; }
}
