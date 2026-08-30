using System;
using System.Collections.Generic;
using MobusTCP.Models;
using MobusTCP.Services.Interfaces;

namespace MobusTCP.Services;

public class KinematicsService : IKinematicsService
{
    private const double Deg2Rad = Math.PI / 180.0;
    private const double Rad2Deg = 180.0 / Math.PI;
    private const double Gravity = 9.80665; // m/s^2

    public List<DhParameterRow> CalculateDhTable(RobotParameters p, double q1Deg, double q2Deg, double q3Deg, double q4Deg)
    {
        var rows = new List<DhParameterRow>
        {
            new()
            {
                LinkIndex = 1,
                JointName = "Base (Yaw Q1)",
                ThetaDeg = q1Deg,
                D = p.L1,
                A = 0.0,
                AlphaDeg = 90.0,
                TransformationMatrixFormatted = FormatMatrix(GetDhTransform(q1Deg * Deg2Rad, p.L1, 0.0, 90.0 * Deg2Rad))
            },
            new()
            {
                LinkIndex = 2,
                JointName = "Shoulder (Pitch Q2)",
                ThetaDeg = q2Deg,
                D = 0.0,
                A = p.L2,
                AlphaDeg = 0.0,
                TransformationMatrixFormatted = FormatMatrix(GetDhTransform(q2Deg * Deg2Rad, 0.0, p.L2, 0.0))
            },
            new()
            {
                LinkIndex = 3,
                JointName = "Elbow (Pitch Q3)",
                ThetaDeg = q3Deg,
                D = 0.0,
                A = p.L3,
                AlphaDeg = 0.0,
                TransformationMatrixFormatted = FormatMatrix(GetDhTransform(q3Deg * Deg2Rad, 0.0, p.L3, 0.0))
            },
            new()
            {
                LinkIndex = 4,
                JointName = "Wrist (Pitch Q4)",
                ThetaDeg = q4Deg,
                D = 0.0,
                A = p.L4,
                AlphaDeg = 0.0,
                TransformationMatrixFormatted = FormatMatrix(GetDhTransform(q4Deg * Deg2Rad, 0.0, p.L4, 0.0))
            }
        };

        return rows;
    }

    private static double[,] GetDhTransform(double theta, double d, double a, double alpha)
    {
        double ct = Math.Cos(theta);
        double st = Math.Sin(theta);
        double ca = Math.Cos(alpha);
        double sa = Math.Sin(alpha);

        return new double[4, 4]
        {
            { ct, -st * ca,  st * sa, a * ct },
            { st,  ct * ca, -ct * sa, a * st },
            { 0,   sa,       ca,      d      },
            { 0,   0,        0,       1      }
        };
    }

    private static double[,] MultiplyMatrices(double[,] a, double[,] b)
    {
        double[,] result = new double[4, 4];
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                double sum = 0;
                for (int k = 0; k < 4; k++)
                {
                    sum += a[i, k] * b[k, j];
                }
                result[i, j] = sum;
            }
        }
        return result;
    }

    private static string FormatMatrix(double[,] m)
    {
        return $"[{m[0,0]:F2}, {m[0,1]:F2}, {m[0,2]:F2}, {m[0,3]:F1}]\n" +
               $"[{m[1,0]:F2}, {m[1,1]:F2}, {m[1,2]:F2}, {m[1,3]:F1}]\n" +
               $"[{m[2,0]:F2}, {m[2,1]:F2}, {m[2,2]:F2}, {m[2,3]:F1}]\n" +
               $"[{m[3,0]:F0}, {m[3,1]:F0}, {m[3,2]:F0}, {m[3,3]:F0}]";
    }

    public (double X, double Y, double Z, double Pitch, double Yaw, double Roll,
            (double X, double Y, double Z) P0,
            (double X, double Y, double Z) P1,
            (double X, double Y, double Z) P2,
            (double X, double Y, double Z) P3,
            (double X, double Y, double Z) P4)
    ForwardKinematics(RobotParameters p, double q1Deg, double q2Deg, double q3Deg, double q4Deg)
    {
        double q1 = q1Deg * Deg2Rad;
        double q2 = q2Deg * Deg2Rad;
        double q3 = q3Deg * Deg2Rad;
        double q4 = q4Deg * Deg2Rad;

        double c1 = Math.Cos(q1);
        double s1 = Math.Sin(q1);

        double q23 = q2 + q3;
        double q234 = q2 + q3 + q4;

        // Position P0 (Base ground origin)
        var p0 = (X: 0.0, Y: 0.0, Z: 0.0);

        // Position P1 (Shoulder joint at base height L1)
        var p1 = (X: 0.0, Y: 0.0, Z: p.L1);

        // Position P2 (Elbow joint)
        double r2 = p.L2 * Math.Cos(q2);
        double z2 = p.L1 + p.L2 * Math.Sin(q2);
        var p2 = (X: r2 * c1, Y: r2 * s1, Z: z2);

        // Position P3 (Wrist joint)
        double r3 = r2 + p.L3 * Math.Cos(q23);
        double z3 = z2 + p.L3 * Math.Sin(q23);
        var p3 = (X: r3 * c1, Y: r3 * s1, Z: z3);

        // Position P4 (End-effector tip)
        double r4 = r3 + p.L4 * Math.Cos(q234);
        double z4 = z3 + p.L4 * Math.Sin(q234);
        var p4 = (X: r4 * c1, Y: r4 * s1, Z: z4);

        double pitch = q234 * Rad2Deg;
        double yaw = q1Deg;
        double roll = 0.0;

        return (p4.X, p4.Y, p4.Z, pitch, yaw, roll, p0, p1, p2, p3, p4);
    }

    public (bool IsReachable, double Q1, double Q2, double Q3, double Q4, string Message)
    InverseKinematics(RobotParameters p, double x, double y, double z, double pitchDeg, bool elbowUp)
    {
        // 1. Base Joint Q1
        double q1 = Math.Atan2(y, x);
        double r = Math.Sqrt(x * x + y * y);

        // 2. Wrist position (rw, zw)
        double phi = pitchDeg * Deg2Rad;
        double rw = r - p.L4 * Math.Cos(phi);
        double zw = z - p.L1 - p.L4 * Math.Sin(phi);

        // 3. Distance from shoulder (0, L1) to wrist (rw, zw)
        double dSquared = rw * rw + zw * zw;
        double d = Math.Sqrt(dSquared);

        double l2 = p.L2;
        double l3 = p.L3;

        // Reachability check
        if (d > (l2 + l3) + 1e-3)
        {
            return (false, 0, 0, 0, 0, $"Target ({x:F1}, {y:F1}, {z:F1}) is outside maximum reachable workspace! (Distance {d:F1}mm > Max {l2 + l3:F1}mm)");
        }
        if (d < Math.Abs(l2 - l3) - 1e-3)
        {
            return (false, 0, 0, 0, 0, $"Target is inside minimum boundary! (Distance {d:F1}mm < Min {Math.Abs(l2 - l3):F1}mm)");
        }

        // 4. Elbow Joint Q3 via Law of Cosines
        double cosQ3 = (dSquared - l2 * l2 - l3 * l3) / (2.0 * l2 * l3);
        cosQ3 = Math.Clamp(cosQ3, -1.0, 1.0);

        double q3 = elbowUp ? Math.Acos(cosQ3) : -Math.Acos(cosQ3);

        // 5. Shoulder Joint Q2
        double alpha = Math.Atan2(zw, rw);
        double beta = Math.Atan2(l3 * Math.Sin(q3), l2 + l3 * Math.Cos(q3));
        double q2 = alpha - beta;

        // 6. Wrist Joint Q4
        double q4 = phi - q2 - q3;

        // Convert to Degrees
        double q1Deg = q1 * Rad2Deg;
        double q2Deg = q2 * Rad2Deg;
        double q3Deg = q3 * Rad2Deg;
        double q4Deg = q4 * Rad2Deg;

        // Normalize to [-180, 180]
        q1Deg = NormalizeAngleDeg(q1Deg);
        q2Deg = NormalizeAngleDeg(q2Deg);
        q3Deg = NormalizeAngleDeg(q3Deg);
        q4Deg = NormalizeAngleDeg(q4Deg);

        // Joint limits check
        if (q1Deg < p.Q1Min || q1Deg > p.Q1Max)
            return (false, q1Deg, q2Deg, q3Deg, q4Deg, $"Joint 1 angle {q1Deg:F1}° exceeds limits [{p.Q1Min}°, {p.Q1Max}°]");
        if (q2Deg < p.Q2Min || q2Deg > p.Q2Max)
            return (false, q1Deg, q2Deg, q3Deg, q4Deg, $"Joint 2 angle {q2Deg:F1}° exceeds limits [{p.Q2Min}°, {p.Q2Max}°]");
        if (q3Deg < p.Q3Min || q3Deg > p.Q3Max)
            return (false, q1Deg, q2Deg, q3Deg, q4Deg, $"Joint 3 angle {q3Deg:F1}° exceeds limits [{p.Q3Min}°, {p.Q3Max}°]");
        if (q4Deg < p.Q4Min || q4Deg > p.Q4Max)
            return (false, q1Deg, q2Deg, q3Deg, q4Deg, $"Joint 4 angle {q4Deg:F1}° exceeds limits [{p.Q4Min}°, {p.Q4Max}°]");

        return (true, q1Deg, q2Deg, q3Deg, q4Deg, "Inverse Kinematics calculated successfully.");
    }

    private static double NormalizeAngleDeg(double angle)
    {
        while (angle > 180.0) angle -= 360.0;
        while (angle < -180.0) angle += 360.0;
        return angle;
    }

    public (double[,] J, double Determinant, double Manipulability, bool IsSingular)
    CalculateJacobian(RobotParameters p, double q1Deg, double q2Deg, double q3Deg, double q4Deg)
    {
        double q1 = q1Deg * Deg2Rad;
        double q2 = q2Deg * Deg2Rad;
        double q3 = q3Deg * Deg2Rad;
        double q4 = q4Deg * Deg2Rad;

        double c1 = Math.Cos(q1);
        double s1 = Math.Sin(q1);

        double q23 = q2 + q3;
        double q234 = q2 + q3 + q4;

        double s2 = Math.Sin(q2);
        double c2 = Math.Cos(q2);
        double s23 = Math.Sin(q23);
        double c23 = Math.Cos(q23);
        double s234 = Math.Sin(q234);
        double c234 = Math.Cos(q234);

        double r = p.L2 * c2 + p.L3 * c23 + p.L4 * c234;

        // Partial derivatives of radius r
        double dr_dq2 = -p.L2 * s2 - p.L3 * s23 - p.L4 * s234;
        double dr_dq3 = -p.L3 * s23 - p.L4 * s234;
        double dr_dq4 = -p.L4 * s234;

        // Partial derivatives of height Z
        double dz_dq2 = p.L2 * c2 + p.L3 * c23 + p.L4 * c234;
        double dz_dq3 = p.L3 * c23 + p.L4 * c234;
        double dz_dq4 = p.L4 * c234;

        // 4x4 Jacobian mapping [q1Dot, q2Dot, q3Dot, q4Dot] -> [xDot, yDot, zDot, pitchDot]
        double[,] j = new double[4, 4]
        {
            { -r * s1, c1 * dr_dq2, c1 * dr_dq3, c1 * dr_dq4 },
            {  r * c1, s1 * dr_dq2, s1 * dr_dq3, s1 * dr_dq4 },
            {  0.0,    dz_dq2,      dz_dq3,      dz_dq4      },
            {  0.0,    1.0,         1.0,         1.0         }
        };

        // Determinant of 4x4 Jacobian:
        // det(J) = r * (L2 * L3 * sin(q3)) in mm^3
        double det = r * (p.L2 * p.L3 * Math.Sin(q3));
        double manipulability = Math.Abs(det) / 1000000.0; // Scaled manipulability index

        bool isSingular = Math.Abs(Math.Sin(q3)) < 0.02 || Math.Abs(r) < 5.0;

        return (j, det, manipulability, isSingular);
    }

    public (double Tau1, double Tau2, double Tau3, double Tau4, double TotalPower)
    CalculateDynamics(RobotParameters p,
                      double q1Deg, double q2Deg, double q3Deg, double q4Deg,
                      double q1DotDeg, double q2DotDeg, double q3DotDeg, double q4DotDeg,
                      double q1DdotDeg, double q2DdotDeg, double q3DdotDeg, double q4DdotDeg)
    {
        // Convert to SI units (rad, rad/s, rad/s^2, meters, kg)
        double q1 = q1Deg * Deg2Rad;
        double q2 = q2Deg * Deg2Rad;
        double q3 = q3Deg * Deg2Rad;
        double q4 = q4Deg * Deg2Rad;

        double q1Dot = q1DotDeg * Deg2Rad;
        double q2Dot = q2DotDeg * Deg2Rad;
        double q3Dot = q3DotDeg * Deg2Rad;
        double q4Dot = q4DotDeg * Deg2Rad;

        double q1Ddot = q1DdotDeg * Deg2Rad;
        double q2Ddot = q2DdotDeg * Deg2Rad;
        double q3Ddot = q3DdotDeg * Deg2Rad;
        double q4Ddot = q4DdotDeg * Deg2Rad;

        double l1 = p.L1 / 1000.0;
        double l2 = p.L2 / 1000.0;
        double l3 = p.L3 / 1000.0;
        double l4 = p.L4 / 1000.0;

        double rc1 = p.Rc1 / 1000.0;
        double rc2 = p.Rc2 / 1000.0;
        double rc3 = p.Rc3 / 1000.0;
        double rc4 = p.Rc4 / 1000.0;

        double m1 = p.M1;
        double m2 = p.M2;
        double m3 = p.M3;
        double m4 = p.M4;

        // Inertias about link CM
        double I1 = (1.0 / 12.0) * m1 * l1 * l1;
        double I2 = (1.0 / 12.0) * m2 * l2 * l2;
        double I3 = (1.0 / 12.0) * m3 * l3 * l3;
        double I4 = (1.0 / 12.0) * m4 * l4 * l4;

        double q23 = q2 + q3;
        double q234 = q2 + q3 + q4;

        // 1. Gravity Torques G(q)
        double g1 = 0.0;
        double g2 = (m2 * rc2 + (m3 + m4) * l2) * Gravity * Math.Cos(q2)
                  + (m3 * rc3 + m4 * l3) * Gravity * Math.Cos(q23)
                  + (m4 * rc4) * Gravity * Math.Cos(q234);

        double g3 = (m3 * rc3 + m4 * l3) * Gravity * Math.Cos(q23)
                  + (m4 * rc4) * Gravity * Math.Cos(q234);

        double g4 = (m4 * rc4) * Gravity * Math.Cos(q234);

        // 2. Inertia Matrix Elements M(q)
        double rProj2 = l2 * Math.Cos(q2);
        double rProj3 = rProj2 + l3 * Math.Cos(q23);
        double rProj4 = rProj3 + l4 * Math.Cos(q234);

        double M11 = I1 + m2 * Math.Pow(rc2 * Math.Cos(q2), 2)
                        + m3 * Math.Pow(rProj2 + rc3 * Math.Cos(q23), 2)
                        + m4 * Math.Pow(rProj3 + rc4 * Math.Cos(q234), 2);

        double M22 = I2 + m2 * rc2 * rc2 + I3 + m3 * (l2 * l2 + rc3 * rc3 + 2 * l2 * rc3 * Math.Cos(q3))
                   + I4 + m4 * (l2 * l2 + l3 * l3 + rc4 * rc4 + 2 * l2 * l3 * Math.Cos(q3)
                                + 2 * l2 * rc4 * Math.Cos(q3 + q4) + 2 * l3 * rc4 * Math.Cos(q4));

        double M33 = I3 + m3 * rc3 * rc3 + I4 + m4 * (l3 * l3 + rc4 * rc4 + 2 * l3 * rc4 * Math.Cos(q4));
        double M44 = I4 + m4 * rc4 * rc4;

        double M23 = I3 + m3 * (rc3 * rc3 + l2 * rc3 * Math.Cos(q3))
                   + I4 + m4 * (l3 * l3 + rc4 * rc4 + l2 * l3 * Math.Cos(q3)
                                + l2 * rc4 * Math.Cos(q3 + q4) + 2 * l3 * rc4 * Math.Cos(q4));

        double M24 = I4 + m4 * (rc4 * rc4 + l2 * rc4 * Math.Cos(q3 + q4) + l3 * rc4 * Math.Cos(q4));
        double M34 = I4 + m4 * (rc4 * rc4 + l3 * rc4 * Math.Cos(q4));

        // 3. Coriolis / Centrifugal Torques C(q, qDot)*qDot
        double c1 = 2.0 * (m2 * rc2 * rc2 * Math.Cos(q2) * (-Math.Sin(q2)) * q2Dot) * q1Dot;
        double c2 = -m3 * l2 * rc3 * Math.Sin(q3) * (2 * q2Dot * q3Dot + q3Dot * q3Dot)
                    -m4 * l2 * l3 * Math.Sin(q3) * (2 * q2Dot * q3Dot + q3Dot * q3Dot)
                    -m4 * l2 * rc4 * Math.Sin(q3 + q4) * (2 * q2Dot * (q3Dot + q4Dot) + Math.Pow(q3Dot + q4Dot, 2));

        double c3 = m3 * l2 * rc3 * Math.Sin(q3) * (q2Dot * q2Dot)
                  + m4 * l2 * l3 * Math.Sin(q3) * (q2Dot * q2Dot)
                  - m4 * l3 * rc4 * Math.Sin(q4) * (2 * (q2Dot + q3Dot) * q4Dot + q4Dot * q4Dot);

        double c4 = m4 * l2 * rc4 * Math.Sin(q3 + q4) * (q2Dot * q2Dot)
                  + m4 * l3 * rc4 * Math.Sin(q4) * Math.Pow(q2Dot + q3Dot, 2);

        // 4. Joint Viscous Friction
        double bFriction = 0.05; // N*m*s/rad

        // Total Joint Torques (Tau = M*qDdot + C*qDot + G + B*qDot)
        double tau1 = M11 * q1Ddot + c1 + g1 + bFriction * q1Dot;
        double tau2 = (M22 * q2Ddot + M23 * q3Ddot + M24 * q4Ddot) + c2 + g2 + bFriction * q2Dot;
        double tau3 = (M23 * q2Ddot + M33 * q3Ddot + M34 * q4Ddot) + c3 + g3 + bFriction * q3Dot;
        double tau4 = (M24 * q2Ddot + M34 * q3Ddot + M44 * q4Ddot) + c4 + g4 + bFriction * q4Dot;

        // Total instantaneous mechanical power
        double totalPower = Math.Abs(tau1 * q1Dot) + Math.Abs(tau2 * q2Dot) + Math.Abs(tau3 * q3Dot) + Math.Abs(tau4 * q4Dot);

        return (tau1, tau2, tau3, tau4, totalPower);
    }
}
