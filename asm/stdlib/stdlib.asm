; source http://www.cplusplus.com/reference/cstdlib/
; this one needs a TON of work
; proper malloc algorithm has been put off until sys_brk is implemented

global atexit, exit, abort

global atoi, atol, atof

global NULL

global malloc, calloc, realloc, free

global RAND_MAX, rand, srand

global abs, labs

; -------------------------------------------

extern isspace

extern pow

; -------------------------------------------

segment .text

; int atexit(void (*func)());
; adds func to the stack of functions to call in exit() before termination.
; returns zero on success.
atexit:
    mov r8, [atexit_dat]
    mov r9d, [atexit_len]
    mov r10d, [atexit_cap]
    
    ; if we're about to exceed capacity
    cmp r9d, r10d
    jb .add
    
    ; allocate more space
    inc r10d
    shl r10d, 1 ; compute new cap = (cap + 1) * 2
    push r10d
    push rdi
    
    shl r10d, 3 ; compute raw memory size = cap * 8
    mov rdi, r8
    mov esi, r10d
    call realloc ; new array in rax
    
    pop rdi
    pop r10d
    
    ; if realloc failed, return nonzero
    cmp rax, 0
    jnz .good
    
    inc rax ; return nonzero (failure)
    ret
    
    .good:
    ; update array and capacity
    mov [atexit_dat], rax
    mov [atexit_cap], r10d
    
    ; now that we have more space, try that again
    jmp atexit
    
    .add:
    ; add it to the stack
    mov [r8 + 8*r9], rdi
    inc r9d
    mov [atexit_len], r9d
    
    xor eax, eax ; return 0 (success)
    ret
; void exit(int status);
; terminates execution with the specified status (return value).
; before termination, invokes all the functions set by calls to atexit() in reverse order.
; it is undefined behavior for these functions to make calls to exit() or atexit().
; it is recommended not to directly call sys_exit, as some of the atexit() cleanup may be important.
exit:
    ; we'll use call-safe registers
    ; no need to preserve them since we won't be returning
    mov r15d, edi          ; status in r15d
    mov r14d, [atexit_len] ; len in r14
    mov r13, [atexit_dat]  ; ptr in r13
    
    ; for(int i = len-1; i >= 0; --i)
    ; -- i = r14d
    dec r14
    jmp .aft
    .loop:
        call [r13 + 8*r14]
        dec r14
    .aft:
        cmp r14, 0
        jge .loop
    
    .done:
    ; terminate with specified status
    mov eax, sys_exit
    mov ebx, r15d
    syscall
    ; program terminated
    
; void abort(void);
; immediately terminates execution and yields an abort error code to the system.
; it is recommended to use exit() instead where possible, as abort performs no cleanup.
abort:
    hlt
    ; program terminated
; -------------------------------------------

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
; align value must be a power of 2
_align:
    ; get position in block (rcx)
    mov rbx, rsi
    dec rbx
    mov rcx, rdi
    and rcx, rbx
    
    ; aligned in rax
    mov rax, rdi
    sub rsi, rcx
    add rax, rsi
    
    ; if pos was zero, return the unaligned instead
    cmp rcx, 0
    movz rax, rdi
    ret

; -------------------------------------------

; the amount of memory for malloc to request at a time
; must be a power of 2
_malloc_step: equ 1 * 1024 * 1024

; <- stack | ([void *next][void *prev][... data ...])...
; pointers will be 8-byte aligned.
; bit 0 of next will hold 1 if this block is occupied.

; void *malloc(qword size);
; allocates contiguous memory in dynamically-allocated space via sys_brk.
; you should not directly call sys_brk at any time (except in the case where it just returns the current break).
; the memory returned is aligned on 8-byte boundaries.
; the pointer returned by this function must later be dealocated by calling free().
; allocating 0 bytes returns null.
; upon failure (e.g. sys_brk refused), returns null.
; upon success, returns a pointer to the allocated memory.
; dereferencing this pointer out of bounds is undefined behavior.
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
    ; for(void *prev = 0, *pos = beg; pos < end; prev = pos, pos = pos->next)
    ; -- prev = r12
    ; -- pos = rsi
    ; -- end = r8
    xor r12, r12
    jmp .aft
    .top:
        ; get the next pointer
        mov rdx, [rsi]
        btr rdx, 0
        
        ; if it's occupied, skip
        jc .cont
        
        ; compute size - if it's not big enough, it won't work
        mov rcx, rdx
        sub rcx, rsi
        sub rcx, 16
        cmp rcx, rdi
        jb .cont
        
        ; -- yay we got one -- ;
        
        ; split the block if it's large enough (that the split block's size > 0)
        ; if (block_size >= request + 24)
        mov rax, rdi
        add rax, 24
        cmp rcx, rax
        jb .nosplit
        
        ; splitting block - get pointer to start of split
        lea rbx, [rsi + 16 + rdi]
        
        ; update split's next/prev (unoccupied)
        mov [rbx], rdx
        mov [rbx + 8], rsi
        
        ; if next is in bounds, update next->prev
        cmp rdx, r8
        movb [rdx + 8], rbx
        
        ; update register that holds our next
        mov rdx, rbx
        
        .nosplit:
        or dl, 1 ; mark this block as occupied
        mov [rsi], rdx
        lea rax, [rsi + 16] ; return pointer to data array
        ret
        
    .cont:
        mov r12, rsi
        mov rsi, rdx
    .aft:
        cmp rsi, r8
        jb .top
    
    ; -- if we got here, we went out of range of the malloc field -- ;
    
    ; put position to add block in r8 (overwrite prev if not in use, otherwise malloc_end)
    cmp r12, 0
    jz .begin_add
    mov rax, [r12]
    bt rax, 0
    movnc r8, r12
    movnc r12, [r8 + 8]
    
    .begin_add:
    ; get program break
    mov eax, sys_brk
    xor ebx, ebx
    syscall
    
    ; if we have room, create a new block on the end and take that
    lea r10, [r8 + 16 + rdi]
    cmp r10, rax
    ja .nospace
    
    .enough_space:
    ; we have enough space - create the new block on the end (occupied)
    mov [malloc_end], r10
    or r10b, 1
    mov [r8], r10
    mov [r8 + 8], r12
    lea rax, [r8 + 16]
    ret
    
    .nospace:
    ; otherwise we have no space - get amount of space to allocate (multiple of step)
    mov r11, rdi
    mov rdi, r10
    sub rdi, rax
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
    
    ; if we got a zero return, we're good
    cmp rax, 0
    jz .enough_space
    ; otherwise it failed - return null
    xor rax, rax
    ret
; void *calloc(qword size);
; as malloc except that it also zeroes the contents.
calloc:
    ; align the size (for later)
    mov esi, 8
    call _align
    push rax
    
    ; allocate the memory
    mov rdi, rax
    call malloc ; array in rax
    pop rcx     ; size in rcx
    
    ; if it returned null, early exit
    cmp rax, 0
    jz .ret
    
    ; zero the contents
    jrcxz .ret ; hopefully redundant, but safer to make sure
    .loop:
        sub rcx, 8
        mov qword ptr [rax + rcx], 0
        jnz .loop
    
    .ret: ret
; void *realloc(void *ptr, qword size);
; creates a new aray with the specified size and copies over the contents.
; the resulting array is identical up to the lesser of the two sizes.
; if posible, the resize is performed in-place.
; returns a pointer to the new array.
; reallocating a null pointer is equivalent to calling malloc()
; reallocating to a size of zero is equivalent to calling free() (and returns null).
realloc:
    ; if pointer is null, call malloc()
    cmp rdi, 0
    jnz .non_null
    mov rdi, rsi
    call malloc
    ret
    .non_null:
    
    ; if size is zero, call free()
    cmp rsi, 0
    jnz .resize
    call free
    xor rax, rax
    ret
    .resize:
    
    ; align the size (for later)
    mov r8, rdi
    mov rdi, rsi
    mov esi, 8
    call _align
    mov rsi, rax ; aligned size back in rsi
    mov rdi, r8  ; pointer back in rdi
    
    ; compute the size of this block
    mov rcx, [rdi - 16]
    and cl, ~1
    mov rbx, rcx ; save a copy of next in rbx
    sub rcx, rdi
    
    ; if we already have enough space, we're good
    cmp rcx, rsi
    jae .ret
    ; compute the smaller size into rcx
    mova rcx, rsi
    
    ; we're going down the route of needing a new array
    ; if next is malloc_end, we can still do it in-place
    cmp rbx, [malloc_end]
    jne .new_array
    
    mov eax, sys_brk
    xor ebx, ebx
    syscall             ; current break point in rax
    lea r8, [rdi + rsi] ; break point needed in r8 (this is why we aligned size earlier)
    
    ; if we have enough room, just move malloc_end
    cmp r8, rax
    ja .more_mem
    
    .good_mem:
    mov [malloc_end], r8
    or r8b, 1
    mov [rdi - 16], r8
    mov rax, rdi
    ret
    
    .more_mem:
    ; otherwise we need to allocate more space
    ; align request size to a multiple of _malloc_step
    mov r10, rdi
    mov r11, rax ; save current break in r11
    mov rdi, r8
    sub rdi, rax
    mov rsi, _malloc_step
    call _align
    ; and allocate that much extra memory
    mov rbx, rax
    add rbx, r11
    mov eax, sys_brk
    syscall
    mov rdi, r10
    
    ; if it succeeded, we're done
    cmp rax, 0
    jz .good_mem
    ; otherwise it failed - return null
    xor rax, rax
    ret
    
    .new_array: ; sad days, everybody, we need a new array
    
    ; otherwise we need a new array
    push rcx
    push rdi
    mov rdi, rsi
    call malloc
    mov rsi, rax ; new array in rsi
    pop rdi      ; old array in rdi
    pop rcx      ; smaller size in rcx
    
    ; copy the contents up to the smaller size
    ; (this is why we aligned the size earlier)
    jrcxz .done ; hopefully refundant but safer to check
    .loop:
        sub rcx, 8
        mov rax, [rdi + rcx]
        mov [rsi + rcx], rax
        jnz .loop
    
    .done:
    ; free the old array
    push rsi
    call free
    pop rax
    ret
    
    .ret:
    mov rax, rdi
    ret
; void free(void *ptr);
; deallocated resources that were allocated by malloc.
; the specified pointer must be exactly what was returned by malloc.
; freeing the same pointer twice is undefined behavior
free:
    ; get the raw pointer
    sub rdi, 16
    
    ; get next in rdx
    mov rdx, [rdi]
    and dl, ~1
    ; if next is in range and not in use, get next->next in rdx (we'll merge right)
    cmp rdx, [malloc_end]
    jae .nomerge_right
    mov rcx, [rdx]
    bt rcx, 0
    movnc rdx, rcx
    .nomerge_right:
    
    ; get prev in rcx
    mov rcx, [rdi + 8]
    ; if prev is in range and not in use, merge with it
    cmp rcx, 0
    jz .nomerge_left
    mov rbx, [rcx]
    bt rbx, 0
    jc .nomerge_left
    mov [rcx], rdx ; this merges left and right simultaneously
    ret
    
    .nomerge_left:
    ; if we're not merging left, we still need to merge right (even if just to mark as not in use)
    mov [rdi], rdx
    ret

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
atexit_dat: resq 1 ; pointer to dynamic array
atexit_len: resd 1 ; number of items in the atexit_dat array
atexit_cap: resd 1 ; capacity of atexit_dat array

align 8
malloc_beg: resq 1 ; starting address for malloc
malloc_end: resq 1 ; stopping address for malloc

align 8
qtemp: resq 1 ; 64-bit temporary
calloc_temp: resq 1
realloc_temp: resq 1

align 4
inext: resd 1 ; these are used in the pseudo-random functions
inextp: resd 1
SeedArray: resd 56
