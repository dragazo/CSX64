; this file serves as the "real" entry point for any program and is required by the linker.
; its purpose is to perform some setup and then call the "main" entry point.
; after "main" returns, it performs some cleanup routines before terminating.

; this file is the first to be merged into the executable by the linker.
; in CSX64, the text segment comes first.
; that means this file's text segment will start at address 0.
; execution of any program will begin at address 0.
; thus, the "real" entry point is whatever's at the top of this file's text segment.

; this file is purely for the linker - it is strongly advised that users DO NOT MODIFY THIS.
; an external named "_start" is required.
; "_start" (only in this file) will be renamed by the linker to whatever the "main" entry point is.

extern _start

extern exit

segment .text
    
    ; call user-defined "main" entry point
    call _start
    
    ; call exit() with the return value
    mov edi, eax
    call exit
