global malloc
global free

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

segment .data

heap_top: dq __heap__ ; top of memory heap