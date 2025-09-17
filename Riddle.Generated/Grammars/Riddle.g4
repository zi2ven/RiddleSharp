grammar Riddle;

compileUnit
    : (packageStmt)? statememt*
    ;
    
statememt
    : varDecl
    | funcDecl
    | exprStmt
    ;

packageStmt
    : Package name=QName
    ;

varDecl
    : Var name=Identifier (Colon type=expression)? (Assign value=expression)? Semi
    ;
    
funcParam
    : name=Identifier Colon type=expression
    ;    

funcDecl
    : Fun name=Identifier LParen (funcParam (Comma funcParam)*)? RParen ('->' type=expression) ((body=block)|Semi)
    ;
    
block
    : LBrace statememt* RBrace
    ;    

exprStmt
    : expression Semi
    ;
    
expression
    : left=expression OP right=expression #binaryOp
    | IntLit #integer
    | QName #symbol
    ;
    
QName: (Identifier Colon Colon)* Identifier;
    
Var: 'var';
Fun: 'fun';
Package: 'package' ;

Semi: ';';
Colon: ':';
Comma: ',';
Assign: '=';
LParen : '(' ;
RParen : ')' ;
LBrace : '{' ;
RBrace : '}' ;

IntLit: [1-9][0-9]* | '0';
Identifier: [a-zA-Z_][a-zA-Z0-9_]*;
OP: [-+*/!%^&~];

WD: [\t\r\n ] -> skip;