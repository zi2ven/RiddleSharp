grammar Riddle;

compileUnit
    : (packageStmt)? (importStmt)* statememt*
    ;
    
statememt
    : block
    | varDecl
    | funcDecl
    | exprStmt
    | returnStmt
    | ifStmt
    | whileStmt
    ;

packageStmt
    : Package name=qName Semi
    ;
    
importStmt
    : Import name=qName Semi
    ;

varDecl
    : Var name=Identifier (Colon type=expression)? (Assign value=expression)? Semi
    ;
    
funcParam
    : name=Identifier Colon type=expression
    ;    

funcDecl
    : Fun name=Identifier LParen (funcParam (Comma funcParam)*)? RParen (Arrow type=expression)? ((body=block)|Semi)
    ;
    
ifStmt
    : If LParen cond=expression RParen then=statememt (Else else=statememt)?
    ;
    
whileStmt
    : While LParen cond=expression RParen body=statememt
    ;
    
block
    : LBrace statememt* RBrace
    ;    

returnStmt
    : Return (result=expression)? Semi
    ;

exprStmt
    : expression Semi
    ;
    
expression
    : callee=expression LParen (args+=expression (Comma args+=expression)*)? RParen #call
    | left=expression op right=expression #binaryOp
    | IntLit #integer
    | qName #symbol
    ;
    
qName
    : (Identifier Colon Colon)* Identifier
    ;   
 
op
    : '+' | '-' | '*' | '/' | '%' | '==' | '!=' | '<' | '>' | '<=' | '>=' 
    | Assign
    ;
    
Var: 'var';
Fun: 'fun';
Package: 'package' ;
Import: 'import' ;
Return: 'return' ;
If: 'if' ;
Else: 'else' ;
While: 'while';

Semi: ';';
Colon: ':';
Comma: ',';
Assign: '=';
LParen : '(' ;
RParen : ')' ;
LBrace : '{' ;
RBrace : '}' ;
Arrow: '->';

IntLit: [1-9][0-9]* | '0';
Identifier: [a-zA-Z_][a-zA-Z0-9_]*;

WD: [\t\r\n ] -> skip;