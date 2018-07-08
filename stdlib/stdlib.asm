; source http://www.cplusplus.com/reference/cstdlib/
; this one needs a TON of work
; proper malloc algorithm has been put off until sys_brk is implemented

global malloc, free

global RAND_MAX, rand, srand

; --------------------

segment .text

; void *malloc(unsigned long size);
malloc:
	; allocating 0 returns null
	cmp rdi, 0
	jnz .begin
	xor rax, rax
	ret
	
	.begin:
	mov rcx, __heap__ ; load starting position (end of program segment)
	jmp .search_aft
	.search_top:
		cmp byte ptr [rcx], 0 ; cmp bucket settings
		jnz .search_inc       ; if nonzero, bucket is in use
		
		cmp [rcx+1], rdi ; cmp bucket size to size requested
		jae .found       ; if large enough, we found one
	.search_inc:
		add rcx, [rcx+1] ; add bucket size
		add rcx, 9       ; add bucket padding
	.search_aft:
		cmp rcx, [heap_top] ; while rcx < [heap_top]
		jb .search_top
	
	; none found, create a new bucket
	mov [rcx+1], rdi     ; set its size
	lea rdx, [rcx+9+rdi] ; increment top past the new bucket
	mov [heap_top], rdx
	
	.found: ; found a spot
		mov byte ptr [rcx], 1 ; mark as used
		lea rax, [rcx+9]      ; store start of bucket data for ret
	.end: ret

; void free(void *ptr);
free:
	cmp rdi, 0 ; cmp 0
	jz .end
	
	mov byte ptr [rdi-9], 0 ; mark as unused
	
	.end: ret

; -------------------------------------------

; pseudo-random - source: https://referencesource.microsoft.com/#mscorlib/system/random.cs

RAND_MAX: equ MBIG ; alias for stdlib macro

MBIG: equ 0x7fffffff
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
        neg esi
        cmp edi, 0
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
            mov [SeedArray + eax*4], ecx
            ; mk = mj - mk;
            neg ecx
            add ecx, ebx
            ; if (mk<0) mk+=MBIG;
            lea r8d, [ecx + MBIG]
            movs ecx, r8d
            ; mj=SeedArray[ii];
            mov ebx, [SeedArray + eax*4]
            
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
                lea eax, [r9d + 30]
                cdq
                idiv dword 55
                inc edx
                mov esi, [SeedArray + r9d*4]
                sub esi, [SeedArray + edx*4]
                mov [SeedArray + r9d*4], esi
                ; if (SeedArray[i]<0) SeedArray[i]+=MBIG;
                lea edx, [esi + MBIG]
                movs [SeedArray + r9d*4], edx
                
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
        mov eax, [SeedArray + ebx*4]
        sub eax, [SeedArray + ecx*4]
        
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
        mov [SeedArray + ebx*4], eax
        
        ; inext = locINext;
        ; inextp = locINextp;
        mov [inext], ebx
        mov [inextp], ecx
        
        ; return retVal;
        ret
    
    
    
    
    
    
    
    
    
    
    
    
    
segment .data

align 8
heap_top: dq __heap__ ; top of memory heap

segment .bss

align 4
inext: resd 1 ; used in pseudo-random functions
inextp: resd 1
SeedArray: resd 56 