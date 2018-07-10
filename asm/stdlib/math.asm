; source http://www.cplusplus.com/reference/cmath/
; needs a lot of work - mostly simple but tedious
; needs asin/acos + ...

global sin, cos, tan
global atan, atan2

global pow, exp, sqrt
global log2, log, log1p, log10

; -----------------------------

segment .text

; -----------------------------

; double sin(double x);
sin:
    movsd [qtemp], xmm0
    fld qword ptr [qtemp]
    fsin
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]
; double cos(double x);
cos:
    movsd [qtemp], xmm0
    fld qword ptr [qtemp]
    fcos
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]
; double tan(double x);
tan:
    movsd [qtemp], xmm0
    fld qword ptr [qtemp]
    fsincos
    fdivp st1, st0
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]

; double atan(double x);
atan:
    movsd [qtemp], xmm0
    fld qword ptr [qtemp]
    fld1
    fpatan
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]
; double atan2(double y, double x);
atan2:
    movsd [qtemp], xmm0
    fld qword ptr [qtemp]
    movsd [qtemp], xmm1
    fld qword ptr [qtemp]
    fpatan
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]

; -----------------------------

; double pow(double base, double exponent);
pow:
    ; we'll compute it as 2^log2(a^b) = 2^(b*log2(a))
    
    ; get args out of xmm and into the fpu
    movsd [qtemp], xmm1
    fld qword ptr [qtemp] ; st1 holds exponent
    movsd [qtemp], xmm0
    fld qword ptr [qtemp] ; st0 holds base
    
    ; do the b*log2(a) part -- only thing on stack now is result
    fyl2x
    
    ; 2^(b*log2(a)) = 2^(i.f) = (2^i) * (2^0.f)
    
    ; compute i
    fld st0
    frndint
    
    ; compute 0.f
    fsub st1, st0
    
    ; compute 2^0.f
    fxch st1
    f2xm1
    fld1
    faddp st1, st0
    
    ; mutiply by 2^i
    fscale
    fstp st1
    
    ; store back in xmm0 for return
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]
    
    ; return result
    ret
; double exp(double x);
exp:
    movsd xmm1, xmm0
    movsd xmm0, [fe]
    call pow
    ret
; double sqrt(double x);
sqrt:
    movsd [qtemp], xmm0
    fld qword ptr [qtemp]
    fsqrt
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]
    ret
    
; -----------------------------------

; double log2(double x);
log2:
    fld1
    movsd [qtemp], xmm0
    fld qword ptr [qtemp]
    fyl2x
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]
    ret
; double log(double x);
log:
    fld1
    fldl2e
    fdivp st1, st0
    movsd [qtemp], xmm0
    fld qword ptr [qtemp]
    fyl2x
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]
    ret
; double log1p(double x);
log1p:
    fld1
    fldl2e
    fdivp st1, st0
    movsd [qtemp], xmm0
    fld qword ptr [qtemp]
    fyl2xp1
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]
    ret
; double log10(double x);
log10:
    fld1
    fldl2t
    fdivp st1, st0
    movsd [qtemp], xmm0
    fld qword ptr [qtemp]
    fyl2x
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]
    ret

segment .rodata

align 8
fe: dq 2.7182818284590452353602874713527 ; not using __e__ in case it's remove later on

segment .bss

align 8
qtemp: resq 1 ; 64-bit temporary
