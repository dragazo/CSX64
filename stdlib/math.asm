; source http://www.cplusplus.com/reference/cmath/
; needs a lot of work - mostly simple but tedious

global pow

segment .text

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
    
    ; set truncation mode
    fstcw [qtemp]
    or word ptr [qtemp], 0xc00
    fldcw [qtemp]
    
    ; compute i
    fld st0
    frndint
    
    ; compute 0.f
    fsub st1, st0
    
    ; compute 2^i
    fld1
    fscale
    fstp st1
    
    ; compute 2^0.f
    fxch st1
    f2xm1
    fld1
    faddp st1, st0
    
    ; multiply the resulting values
    fmulp st1, st0
    
    ; store back in xmm0 for return
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]
    
    ; return result
    ret
    
segment .bss

align 8
qtemp: resq 1 ; 64-bit temporary