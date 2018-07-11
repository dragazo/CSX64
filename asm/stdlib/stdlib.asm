; source http://www.cplusplus.com/reference/cstdlib/
; this one needs a TON of work
; proper malloc algorithm has been put off until sys_brk is implemented

global atoi, atol, atof

global NULL

global malloc, free

global RAND_MAX, rand, srand

global abs, labs

; -------------------------------------------

extern isspace

extern pow

; -------------------------------------------

segment .text

; const char *skipwspace(const char *str);
skipwspace:
    ; reserve r15 for str
    push r15
    mov r15, rdi
    
    ; skip white space in str
    .skip:
        xor edi, edi
        mov dil, [r15]
        call isspace
        
        cmp eax, 0
        jz .skip_done
        
        inc r15
        jmp .skip
    .skip_done:
    
    ; restore register states
    mov rax, r15
    pop r15
    
    ret
    
; helper function
_atoi_init:
    ; skip white space
    call skipwspace
    mov rdi, rax
    
    ; read optional +- sign -- dl = sign
    mov dl, [rdi]
    cmp dl, '+'
    je .pos
    cmp dl, '-'
    je .neg
    jmp .neither
    
    .pos:
    mov bl, 0
    inc rdi
    jmp .aft
    .neg:
    mov bl, 1
    inc rdi
    jmp .aft
    .neither:
    mov bl, 0
    
    .aft:
    xor rax, rax ; clear result
    
    ret
    
; int atoi(const char *str);
atoi:
    call _atoi_init ; initialize    
    
    ; parse the value
    .top:
        ; read a char (zero extended)
        movzx edx, byte ptr [rdi]
        
        ; must be a digit -- value in edx
        cmp dl, '9'
        ja .done
        sub dl, '0'
        jb .done
        
        imul eax, 10
        add eax, edx
        
        inc rdi
        jmp .top 
    .done:
    
    ; account for sign
    mov edx, eax
    neg edx
    cmp bl, 0
    movnz eax, edx
    
    ; return result
    ret
    
; long atol(const char *str);
atol:
    call _atoi_init ; initialize    
    
    ; parse the value
    .top:
        ; read a char (zero extended)
        movzx rdx, byte ptr [rdi]
        
        ; must be a digit -- value in rdx
        cmp dl, '9'
        ja .done
        sub dl, '0'
        jb .done
        
        imul rax, 10
        add rax, rdx
        
        inc rdi
        jmp .top 
    .done:
    
    ; account for sign
    mov rdx, rax
    neg rdx
    cmp bl, 0
    movnz rax, rdx
    
    ; return result
    ret

; helper function
_atof_frac:
    fldz                ; st2 is fractional component
    fld qword ptr [f10] ; st1 holds 10.0
    fld1                ; st0 holds digit scalar (0.1 -> 0.01 -> 0.001 etc.)
    
    ; parse the value
    .top:
        ; read a char (zero extended)
        movzx edx, byte ptr [rdi]
        
        ; must be a digit -- value in edx
        cmp dl, '9'
        ja .done
        sub dl, '0'
        jb .done
        
        fdiv st0, st1
        mov [qtemp], edx
        fild dword ptr [qtemp]
        fmul st0, st1
        faddp st3, st0
        
        inc rdi
        jmp .top 
    .done:
    
    ; pop st0 and st1 (mult masks)
    fstp st0
    fstp st0
    
    ; add fractional component to integral component
    faddp st1, st0
    
    ret
; helper function
_atof_exp:
    ; read the exponent - integral
    push rdi
    call atoi
    pop rdi
    
    ; perform 10^exp
    mov [qtemp], eax
    fild dword ptr [qtemp]
    fstp qword ptr [qtemp]
    movsd xmm0, [f10]
    movsd xmm1, [qtemp]
    call pow
    
    ; multiply into result
    movsd [qtemp], xmm0
    fld qword ptr [qtemp]
    fmulp st1, st0
    
    ret
; double atof(const char *str);
atof:
    call _atoi_init ; initialize    
    
    fldz                ; st1 is integral component
    fld qword ptr [f10] ; st0 holds 10.0
    
    ; parse the value
    .top:
        ; read a char (zero extended)
        movzx edx, byte ptr [rdi]
        
        ; must be a digit -- value in edx
        cmp dl, '9'
        ja .done
        sub dl, '0'
        jb .done
        
        fmul st1, st0
        mov [qtemp], edx
        fild dword ptr [qtemp]
        faddp st2, st0
        
        inc rdi
        jmp .top 
    .done:
    
    ; pop st0 (mult mask)
    fstp st0
    
    ; account for sign
    fld st0
    fchs
    cmp bl, 0
    fmove st0, st1
    fstp st1
    
    ; examine the current character (need to reload because of the sub breaker)
    mov dl, [rdi]
    ; if it's a '.', do frac helper
    cmp dl, '.'
    jne .no_frac
    
    inc rdi
    call _atof_frac
    
    .no_frac:
    ; examine the current character (need to reload because of the potential call)
    mov dl, [rdi]
    ; if it's an 'e' or 'E', do exp helper
    or dl, 32
    cmp dl, 'e' ; convert to lower and just test that
    jne .no_exp
    
    inc rdi
    call _atof_exp
    
    .no_exp:
    
    ; return result
    .ret:
    fstp qword ptr [qtemp]
    movsd xmm0, [qtemp]
    ret
    
; -------------------------------------------

NULL: equ 0

; void *align(void *ptr, ulong align);
_align:
    ; get position in block (rdx)
    mov rax, rdi
    xor rdx, rdx
    div rsi
    
    ; if at 0, already aligned
    cmp rdx, 0
    jz .ret
    
    ; otherwise align it
    sub rsi, rdx
    add rdi, rsi
    
    .ret:
    mov rax, rdi
    ret

; -------------------------------------------

_malloc_step: equ 1 * 1024 * 1024 ; the amount of memory for malloc to request at a time

; <- stack | ([qword size][... data ...])...

; void *malloc(qword size);
malloc:
    ; allocating 0 returns null
    cmp rdi, 0
    jnz .beg
    xor rax, rax
    ret
    
    .beg:
    ; align request size to 8-byte blocks
    mov esi, 8
    call _align
    mov rdi, rax
    
    ; get the beg/end positions
    mov rsi, [malloc_beg]
    mov r8, [malloc_end]
    ; if beg was nonzero, we're good
    cmp rsi, 0
    jnz .ok
    
    ; otherwise initialize beg/end and reload 
    mov r11, rdi
    mov eax, sys_brk
    xor ebx, ebx
    syscall
    mov rdi, rax
    mov esi, 8
    call _align
    mov [malloc_beg], rax
    mov [malloc_end], rax
    mov rsi, rax
    mov r8, rax
    mov rdi, r11
    
    .ok:
    ; look through the list for an available block of sufficient size
    ; for(void *pos = beg; pos < end; pos += 8 + *(qword*)pos)
    ; -- pos = rsi
    ; -- end = r8
    jmp .aft
    .top:
        ; get the block size
        mov rdx, [rsi]
        mov rcx, rdx
        
        ; if it's positive (occupied) - skip
        neg rcx
        js .cont
        ; otherwise it's negative (unoccupied) - put positive in rdx
        mov rdx, rcx
        
        ; if it's not big enough, it won't work
        cmp rdx, rdi
        jb .cont
        
        ; -- yay we got one -- ;
        
        ; split the block if it's large enough and we're not taking much
        ; if (block_size >= 64 && request < block_size / 2)
        cmp rdx, 64
        jb .nosplit
        mov rcx, rdx
        shr rcx, 1
        cmp rdi, rcx
        jae .nosplit
        
        ; splitting block - get pointer to start of split
        lea rbx, [rsi + 8 + rdi]
        
        ; mark split's size (unoccupied)
        sub rdx, rdi
        sub rdx, 8
        neg rdx
        mov [rbx], rdx
        
        ; mark our size (occupied)
        mov [rsi], rdi
        
        .nosplit:
        lea rax, [rsi + 8]
        ret
        
    .cont:
        lea rsi, [rsi + 8 + rdx]
    .aft:
        cmp rsi, r8
        jb .top
    
    ; -- if we got here, we went out of range of the malloc field -- ;
    
    ; get program break
    mov eax, sys_brk
    xor ebx, ebx
    syscall
    
    ; if we have room, create a new block on the end and take that
    lea r10, [r8 + 8 + rdi]
    cmp r10, rax
    ja .nospace
    
    .enough_space:
    ; we have enough space - create the new block on the end (occupied)
    mov [malloc_end], r10
    mov [r8], rdi
    lea rax, [r8 + 8]
    ret
    
    .nospace:
    ; otherwise we have no space - get amount of space to allocate (multiple of step)
    mov r11, rdi
    mov rsi, _malloc_step
    call _align
    mov rdi, r11
    
    ; add that much memory
    mov r9, rax
    mov eax, sys_brk
    xor ebx, ebx
    syscall
    mov rbx, rax
    add rbx, r9
    mov eax, sys_brk
    syscall
    
    ; if we got a nonzero return, return null (bad stuff)
    cmp rax, 0
    jnz .bad_stuff
    
    ; otherwise, go back to the case where we had enough space
    jmp .enough_space
    
    .bad_stuff:
    xor rax, rax
    ret

; void free(void *ptr);
free:
    mov eax, sys_exit ; not implemented yet
    mov ebx, 999
    syscall
    ret

; -------------------------------------------

; void abort(void);
abort:
    mov eax, sys_exit
    mov ebx, err_abort
    syscall

; -------------------------------------------

; pseudo-random - source: https://referencesource.microsoft.com/#mscorlib/system/random.cs

RAND_MAX: equ MBIG ; alias for stdlib macro

MBIG: equ 0x7fffffff ; constants used by rand algorithm
MSEED: equ 161803398
MZ: equ 0

; void srand(unsigned int seed);
srand:
        ; int ii;
        ; int mj, mk;
        ; -- ii = eax
        ; -- mj = ebx
        ; -- mk = ecx
        
        ; Initialize our Seed array.
        ; This algorithm comes from Numerical Recipes in C (2nd Ed.)
        
        ; int subtraction = (Seed == Int32.MinValue) ? Int32.MaxValue : Math.Abs(Seed);
        mov esi, edi
        neg edi
        movs edi, esi
        cmp edi, 0
        movs edi, 0x7fffffff
        ; mj = MSEED - subtraction;
        mov ebx, MSEED
        sub ebx, edi
        ; SeedArray[55]=mj;
        mov [SeedArray + 55*4], ebx
        ; mk=1;
        mov ecx, 1
        
        ; for (int i=1; i<55; i++) {  //Apparently the range [1..55] is special (Knuth) and so we're wasting the 0'th position.
        ; -- i = esi
        mov esi, 1
        .loop1_top:
            ; ii = (21*i)%55;
            imul eax, esi, 21
            cdq
            idiv dword 55
            mov eax, edx
            ; SeedArray[ii]=mk;
            mov [SeedArray + rax*4], ecx
            ; mk = mj - mk;
            neg ecx
            add ecx, ebx
            ; if (mk<0) mk+=MBIG;
            lea r8d, [rcx + MBIG]
            movs ecx, r8d
            ; mj=SeedArray[ii];
            mov ebx, [SeedArray + rax*4]
            
            ; -- looper
            inc esi
            cmp esi, 55
            jl .loop1_top
        ; -- end loop
        
        ; for (int k=1; k<5; k++) {
            ; for (int i=1; i<56; i++) {
            ; -- k = r8d
            ; -- i = r9d
            
        mov r8d, 1
        .loop2_top:
            mov r9d, 1
            .loop3_top:
                ; SeedArray[i] -= SeedArray[1+(i+30)%55];
                lea eax, [r9 + 30]
                cdq
                idiv dword 55
                inc edx
                mov esi, [SeedArray + r9*4]
                sub esi, [SeedArray + rdx*4]
                mov [SeedArray + r9*4], esi
                ; if (SeedArray[i]<0) SeedArray[i]+=MBIG;
                lea edx, [rsi + MBIG]
                movs [SeedArray + r9*4], edx
                
                ; -- looper
                inc r9d
                cmp r9d, 56
                jl .loop3_top
            ; -- end loop
            ; -- looper
            inc r8d
            cmp r8d, 5
            jl .loop2_top
        ; -- end loop
        
        ; inext=0;
        mov dword ptr [inext], 0
        ; inextp = 21;
        mov dword ptr [inextp], 21
        ; Seed = 1; -- not sure why this is here, but leaving it

; int rand(void);
rand:
        ; int retVal;
        ; int locINext = inext;
        ; int locINextp = inextp;
        ; -- retval = eax
        ; -- locINext = ebx
        ; -- locINextp = ecx
        mov ebx, [inext]
        mov ecx, [inextp]
        
        ; if (++locINext >=56) locINext=1;
        inc ebx
        cmp ebx, 56
        movge ebx, 1
        ; if (++locINextp>= 56) locINextp = 1;
        inc ecx
        cmp ecx, 56
        movge ecx, 1
        
        ; retVal = SeedArray[locINext]-SeedArray[locINextp];
        mov eax, [SeedArray + rbx*4]
        sub eax, [SeedArray + rcx*4]
        
        ; if (retVal == MBIG) retVal--;
        mov esi, eax
        dec esi
        cmp eax, MBIG
        move eax, esi
        ; if (retVal<0) retVal+=MBIG;
        mov esi, eax
        add esi, MBIG
        cmp eax, 0
        movl eax, esi
        
        ; SeedArray[locINext]=retVal;
        mov [SeedArray + rbx*4], eax
        
        ; inext = locINext;
        ; inextp = locINextp;
        mov [inext], ebx
        mov [inextp], ecx
        
        ; return retVal;
        ret

; -------------------------------------------

; int abs(int n);
abs:
    mov eax, edi
    neg eax
    movs eax, edi
    ret
; long labs(long n);
labs:
    mov rax, rdi
    neg rax
    movs rax, rdi
    ret

segment .rodata

align 8
f10: dq 10.0
f10th: dq 0.1

segment .bss

align 8
malloc_beg: resq 1 ; starting address for malloc
malloc_end: resq 1 ; stopping address for malloc

align 8
qtemp: resq 1 ; 64-bit temporary

align 4
inext: resd 1 ; these are used in the pseudo-random functions
inextp: resd 1
SeedArray: resd 56
