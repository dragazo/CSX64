global malloc
global free

heap_top: emit __prog_end__ # top of memory heap

# ($1:64 size) -> ($0:64 ptr or null if fail)
# allocates a number of bytes of contiguous dynamic storage space
# returns a pointer to the start of the allocated data
# indexing outside of this range is undefined behavior
malloc:
	# cannot allocate 0 bytes
	cmpz $1
	jnz [.begin]
	xor $0, $0
	jmp [.end]
	
	.begin:
	mov $2, __prog_end__ # load starting position (end of program segment)
	jmp [.search_aft]
	.search_top:
		cmpz:8 [$2]       # cmp0 bucket settings
		jnz [.search_inc] # if nonzero, bucket is in use
		
		cmp [$2+1], $1 # cmp bucket size to size requested
		jae [.found]   # if large enough, we found one
	.search_inc:
		add $2, [$2+1] # add bucket size
		add $2, 9      # add bucket padding
	.search_aft:
		cmp $2, [heap_top] # while $2 < [heap_top]
		jb [.search_top]
	
	# none found, create a new bucket
	mov [$2+1], $1 # set its size
	la $3, [$2+9+$1] # increment top past the new bucket
	mov [heap_top], $3
	
	.found: # found a spot
		mov:8 [$2], 1 # mark as used
		la $0, [$2+9] # store start of bucket data for ret
	.end: ret

# ($1:64 address) -> ()
# frees the memory provided by malloc
# undefined if used on anything not returned by malloc
# noop on null
free:
	cmpz $1 # cmp 0
	jz [.end]
	
	mov:8 [$1-9], 0 # mark as unused
	
	.end: ret
    
    
    
    
    
    
    
    
    
    
    