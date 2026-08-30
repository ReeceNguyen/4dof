# ROBOT Parameters

## Link, limit angle offset

$$\begin{align*}
a_2 &= a_3 = 12 cm \\[6pt]
a_4 &= 3cm \\[6pt]
\triangle \theta_1 ^0 &= -90 \\[6pt]
\triangle \theta_2 ^0 &= 20 \\[6pt]
\triangle \theta_3 ^0 &= 45
\end{align*}
$$

## Forward Kinematics

### D_H TABLE

| i | $a_i$ | $\alpha_i$ | $\theta_i$ | $d_i$ |
|---|-------|------------|------------|-------|
| 1 | 0     | 90         | $\theta_1$ | $d_1$ |
| 2 | $a_2$ | 0          | $\theta_2$ | 0     |
| 3 | $a_3$ | 0          | $\theta_3$ | 0     |
| 4 | $a_4$ | 0          | $\theta_4$ | 0     |

$$
A_1 = 
    \begin{bmatrix}
        c_1 & 0 & s_1 & 0 \\[6pt]
        s_1 & 0 & -c_1 & 0 \\[6pt]
        0 & 1 & 0 & d_1 \\[6pt]
        0 & 0 & 0 & 1
    \end{bmatrix} \quad
A_2 =
    \begin{bmatrix}
        c_2 & -s_2 & 0 & a_2 c_2\\[6pt]
        s_2 &  c_2 & 0 & a_2 s_2\\[6pt]
        0 & 0 & 1 & 0 \\[6pt]
        0 & 0 & 0 & 1
    \end{bmatrix}
A_3 =
    \begin{bmatrix}
        c_3 & -s_3 & 0 & a_3 c_3\\[6pt]
        s_3 &  c_3 & 0 & a_3 s_3\\[6pt]
        0 & 0 & 1 & 0 \\[6pt]
        0 & 0 & 0 & 1
    \end{bmatrix}
A_4 =
    \begin{bmatrix}
        c_4 & -s_4 & 0 & a_4 c_4\\[6pt]
        s_4 &  c_4 & 0 & a_4 s_4\\[6pt]
        0 & 0 & 1 & 0 \\[6pt]
        0 & 0 & 0 & 1
    \end{bmatrix}
$$

$$
T_4^0 = 
    \begin{bmatrix}
        c_1 c_{234} & -c_1 s_{234} & s_1 & c_1 (a_2 c_2 + a_3 c_{23} + a_4 c_{234})\\[6pt]
        s_1 c_{234} & -s_1 s_{234} & -c_1 & s_1 (a_2 c_2 + a_3 c_{23} + a_4 c_{234})\\[6pt]
        s_{234} & c_{234} & 0 & d_1 + a_2 s_2 + a_3 s_{23} + a_4 s_{234}\\[6pt]
        0 & 0 & 0 & 1
    \end{bmatrix} \quad
\theta_{234}=\theta_2 + \theta_3 + \theta_4
$$

## Revert Kinematics

### Tìm $\theta_1$
$$
\begin{align*}
VT1 &= A_{1}^{-1}.T_4^0=
\begin{bmatrix}
    c_1 & s_1 & 0 & 0\\[6pt]
    0 & 0 & 1 & -d_1\\[6pt]
    s_1 & -c_1 & 0 & 0\\[6pt]
    0 & 0 & 0 & 1
\end{bmatrix}
\begin{bmatrix}
    n_x & o_x & a_x & p_x \\[6pt]
    n_y & o_y & a_y & p_y \\[6pt]
    n_z & o_z & a_z & p_z \\[6pt]
    0&0&0&1
\end{bmatrix}
=
\begin{bmatrix}
    c_1 n_x + s_1 n_y & c_1 o_x + s_1 o_y & c_1 a_x + s_1 a_y & c_1 p_x + s_1 p_y \\[6pt]
    n_z & o_z & a_z & p_z - d_1\\[6pt]
    s_1 n_x - c_1 n_y & s_1 o_x - c_1 o_y & s_1 a_x - c_1 a_y & s_1 p_x - c_1 p_y \\[6pt]
    0&0&0&1
\end{bmatrix} \\[6pt]
VF1 &= A_2 A_3 A_4=
\begin{bmatrix}
    c_{234} & -s_{234} & 0 & a_2 c_2 + a_3 c_{23} + a_4 c_{234}\\[6pt]
    s_{234} & c_{234} & 0 & a_2 s_2 + a_3 s_{23} + a_4 s_{234}\\[6pt]
    0 & 0 & 1 & 0\\[6pt]
    0 & 0 & 0 & 1
\end{bmatrix}
\end{align*}
$$
$$\boxed{
\begin{align*}
    \theta_1 &= ATAN2(p_y,p_x)\\[6pt]
    \theta_{234} &= ATAN2(n_z , c_1 n_x + s_1 n_y)
\end{align*}}
$$

### Tìm $\theta_2 , theta_3$

$$
\begin{cases}
    X' = c_1 p_x + s_1 p_y - a_4 = a_2 c_2 + a_3 c_23\\[6pt]
    Z' = p_z - d_1 = a_2 s_2 + a_3 s_23
\end{cases}
$$

$$\begin{align*}
    \cos \theta_3 &=  \frac{X'^2 + Z'^2 - a_2^2 - a_3^2}{2 a_2 a_3}\\[6pt]
    &= \frac{(c_1 p_x + s_1 p_y - a_4 c_{234})^2 + (p_z - d_1 - a_4 s_{234})^2 -a_2^2 - a_3^2}{2 a_2 a_3} 
\end{align*}
$$

$$\begin{align*}
\theta_3 &= ATAN2(\pm \sqrt{1 - \cos^2 \theta_3},\cos \theta_3) \\[6pt]
\theta_2 &= ATAN2(p_z - d_1 - a_4 s_{234},c_1 p_x + s_1 p_y - a_4 c_{234}) - ATAN2(a_3 s_3, a_2 + a_3 c_3)\\[6pt]
\theta_4 &= \theta_{234} - \theta_2 - \theta_3
\end{align*}
$$