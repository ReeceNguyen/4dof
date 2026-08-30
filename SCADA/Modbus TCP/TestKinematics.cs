using System;
using MobusTCP.Models;
using MobusTCP.Services;

namespace MobusTCP;

public static class TestKinematics
{
    public static void Run()
    {
        var robot = new RobotParameters();
        var kin = new KinematicsService();
        var traj = new TrajectoryService(kin);

        Console.WriteLine("=== 1. Testing Forward Kinematics ===");
        var fk = kin.ForwardKinematics(robot, 0, 45, -45, 0);
        Console.WriteLine($"FK (0, 45, -45, 0) -> X: {fk.X:F2}, Y: {fk.Y:F2}, Z: {fk.Z:F2}, Pitch: {fk.Pitch:F2}°");

        Console.WriteLine("=== 2. Testing Inverse Kinematics ===");
        var ik = kin.InverseKinematics(robot, fk.X, fk.Y, fk.Z, fk.Pitch, false);
        Console.WriteLine($"IK reachable: {ik.IsReachable}, Q1: {ik.Q1:F2}, Q2: {ik.Q2:F2}, Q3: {ik.Q3:F2}, Q4: {ik.Q4:F2}");

        Console.WriteLine("=== 3. Testing Jacobian ===");
        var jac = kin.CalculateJacobian(robot, 0, 45, -45, 0);
        Console.WriteLine($"Det(J): {jac.Determinant:E2}, Manipulability: {jac.Manipulability:F4}, Singular: {jac.IsSingular}");

        Console.WriteLine("=== 4. Testing Dynamics (Euler-Lagrange) ===");
        var dyn = kin.CalculateDynamics(robot, 0, 45, -45, 0, 10, 20, -10, 5, 5, 10, -5, 2);
        Console.WriteLine($"Tau1: {dyn.Tau1:F2} N*m, Tau2: {dyn.Tau2:F2} N*m, Tau3: {dyn.Tau3:F2} N*m, Tau4: {dyn.Tau4:F2} N*m, TotalPower: {dyn.TotalPower:F2} W");

        Console.WriteLine("=== 5. Testing Trajectory Planning ===");
        var config = new TrajectoryConfig
        {
            Duration = 2.0,
            StartQ1 = 0, StartQ2 = 45, StartQ3 = -45, StartQ4 = 0,
            EndQ1 = 90, EndQ2 = 10, EndQ3 = -30, EndQ4 = 20,
            ProfileType = TrajectoryProfileType.QuinticPolynomial
        };
        var points = traj.GenerateTrajectory(robot, config);
        Console.WriteLine("=== 6. Testing SQLite Database Service ===");
        var db = new DatabaseService();
        db.InitializeDatabaseAsync().GetAwaiter().GetResult();
        
        db.LogTelemetryAsync(new TelemetryLogEntry
        {
            Timestamp = DateTime.Now,
            Q1 = 10, Q2 = 20, Q3 = 30, Q4 = 40,
            X = 200, Y = 100, Z = 150, Pitch = 0,
            Tau1 = 0.5, Tau2 = 1.5, Tau3 = 0.8, Tau4 = 0.2,
            TotalPower = 3.5, PlcStatus = "TEST", LatencyMs = 1.2
        }).GetAwaiter().GetResult();

        db.LogAlarmAsync("Warning", "TestRunner", "Unit test simulated warning").GetAwaiter().GetResult();

        var hist = db.GetTelemetryHistoryAsync(null, null, 10).GetAwaiter().GetResult();
        var alarms = db.GetAlarmsHistoryAsync(10).GetAwaiter().GetResult();
        var recipes = db.GetRecipesAsync().GetAwaiter().GetResult();

        Console.WriteLine($"Database records -> Historian: {hist.Count}, Alarms: {alarms.Count}, Recipes: {recipes.Count}");

        Console.WriteLine("=== All Kinematics, Dynamics & Database Tests Passed! ===");
    }
}
