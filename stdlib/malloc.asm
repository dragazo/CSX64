global malloc
global free

; (rdi:64 size) -> (rax:64 ptr or null if fail)
; allocates a number of bytes of contiguous dynamic storage space
; returns a pointer to the start of the allocated data
; indexing outside of this range is undefined behavior
malloc:
	; cannot allocate 0 bytes
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

; (rdi:64 address) -> ()
; frees the memory provided by malloc
; undefined if used on anything not returned by malloc
; noop on null
free:
	cmp rdi, 0 ; cmp 0
	jz .end
	
	mov byte ptr [rdi-9], 0 ; mark as unused
	
	.end: ret

segment .data

heap_top: dq __heap__ ; top of memory heap